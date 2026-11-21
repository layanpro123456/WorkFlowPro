using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class TaskEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TaskEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string Token, Guid UserId)> CreateAuthenticatedClientAsync(
        string email,
        string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = email.Split('@')[0],
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await userManager.CreateAsync(user, "Password@123");
            await userManager.AddToRoleAsync(user, role);
        }

        var loginClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginRes = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Password@123"));
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return (client, auth.AccessToken, user.Id);
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client, string prefix = "TSK")
    {
        var code = $"{prefix}_{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new CreateProjectRequest(code, $"Project {code}", "Description", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var response = await client.PostAsJsonAsync("/api/v1/projects", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ProjectSummaryDto>();
        return created!.Id;
    }

    [Fact]
    public async Task CreateTask_ByProjectMember_Returns201_AndCreatesInitialHistory()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr1@workflowpro.local", Roles.Manager);
        var (memberClient, _, memberId) = await CreateAuthenticatedClientAsync("task_mem1@workflowpro.local", Roles.Employee);

        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK1");

        // Add member to project
        await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/members",
            new AddProjectMemberRequest(memberId, ProjectRoles.Member));

        // Member creates task
        var createRequest = new CreateTaskRequest(
            Title: "Setup Database Schemas",
            Description: "Configure EF Core PostgreSQL migrations",
            Priority: TaskPriorities.High,
            AssigneeId: memberId,
            DueAtUtc: DateTime.UtcNow.AddDays(7),
            EstimateMinutes: 120
        );

        var response = await memberClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", createRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<TaskSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal("Setup Database Schemas", summary.Title);
        Assert.Equal(TaskStatuses.Backlog, summary.Status);
        Assert.Equal(1, summary.Version);
        Assert.Equal(memberId, summary.AssigneeId);

        // Fetch detail and verify history
        var detailRes = await memberClient.GetAsync($"/api/v1/tasks/{summary.Id}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = await detailRes.Content.ReadFromJsonAsync<TaskDetailDto>();
        Assert.NotNull(detail);
        Assert.Single(detail.History);
        Assert.Null(detail.History[0].FromStatus);
        Assert.Equal(TaskStatuses.Backlog, detail.History[0].ToStatus);
    }

    [Fact]
    public async Task CreateTask_WithInvalidAssignee_Returns400BadRequest()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr2@workflowpro.local", Roles.Manager);
        var (_, _, nonMemberId) = await CreateAuthenticatedClientAsync("task_nonmem2@workflowpro.local", Roles.Employee);

        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK2");

        var createRequest = new CreateTaskRequest(
            Title: "Invalid Assignee Task",
            Description: null,
            Priority: TaskPriorities.Normal,
            AssigneeId: nonMemberId, // Not a member of this project
            DueAtUtc: null,
            EstimateMinutes: null
        );

        var response = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", createRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Assignee must be an active project member", content);
    }

    [Fact]
    public async Task CreateTask_ByNonMember_Returns404NotFound()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr3@workflowpro.local", Roles.Manager);
        var (outsiderClient, _, _) = await CreateAuthenticatedClientAsync("task_outsider3@workflowpro.local", Roles.Employee);

        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK3");

        var createRequest = new CreateTaskRequest(
            Title: "Unauthorized Task Creation",
            Description: null,
            Priority: TaskPriorities.Low,
            AssigneeId: null,
            DueAtUtc: null,
            EstimateMinutes: null
        );

        var response = await outsiderClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", createRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithMatchingVersion_SucceedsAndIncrementsVersion()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr4@workflowpro.local", Roles.Manager);
        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK4");

        var createRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Original Title", null, TaskPriorities.Normal, null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        var updateRequest = new UpdateTaskRequest(
            Title: "Updated Task Title",
            Description: "Added detailed description",
            Priority: TaskPriorities.Urgent,
            AssigneeId: null,
            DueAtUtc: null,
            EstimateMinutes: 45,
            Version: created!.Version // version 1
        );

        var updateRes = await mgrClient.PutAsJsonAsync($"/api/v1/tasks/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<TaskSummaryDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Task Title", updated.Title);
        Assert.Equal(TaskPriorities.Urgent, updated.Priority);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task UpdateTask_WithStaleVersion_Returns409Conflict()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr5@workflowpro.local", Roles.Manager);
        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK5");

        var createRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Task Concurrency Test", null, TaskPriorities.Normal, null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        // First update bumps version to 2
        await mgrClient.PutAsJsonAsync($"/api/v1/tasks/{created!.Id}",
            new UpdateTaskRequest("Update 1", null, TaskPriorities.Normal, null, null, null, Version: 1));

        // Stale update still presenting version 1
        var staleRes = await mgrClient.PutAsJsonAsync($"/api/v1/tasks/{created.Id}",
            new UpdateTaskRequest("Stale Update", null, TaskPriorities.Normal, null, null, null, Version: 1));

        Assert.Equal(HttpStatusCode.Conflict, staleRes.StatusCode);
        var content = await staleRes.Content.ReadAsStringAsync();
        Assert.Contains("conflict", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeStatus_ValidTransition_AppendsHistory()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr6@workflowpro.local", Roles.Manager);
        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK6");

        var createRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Task Status Flow", null, TaskPriorities.Normal, null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        // Backlog -> ToDo
        var statusRes = await mgrClient.PatchAsJsonAsync($"/api/v1/tasks/{created!.Id}/status",
            new ChangeTaskStatusRequest(TaskStatuses.ToDo, Version: 1, Comment: "Ready for sprint"));
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

        // Fetch history
        var historyRes = await mgrClient.GetAsync($"/api/v1/tasks/{created.Id}/history");
        Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);
        var history = await historyRes.Content.ReadFromJsonAsync<List<TaskStatusHistoryDto>>();

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(TaskStatuses.ToDo, history[0].ToStatus);
        Assert.Equal(TaskStatuses.Backlog, history[0].FromStatus);
        Assert.Equal("Ready for sprint", history[0].Comment);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_Returns400BadRequest()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr7@workflowpro.local", Roles.Manager);
        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK7");

        var createRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Direct Backlog to Done Test", null, TaskPriorities.Normal, null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        // Backlog -> Done is invalid
        var statusRes = await mgrClient.PatchAsJsonAsync($"/api/v1/tasks/{created!.Id}/status",
            new ChangeTaskStatusRequest(TaskStatuses.Done, Version: 1));

        Assert.Equal(HttpStatusCode.BadRequest, statusRes.StatusCode);
        var content = await statusRes.Content.ReadAsStringAsync();
        Assert.Contains("Invalid status transition", content);
    }

    [Fact]
    public async Task AddComment_SucceedsAndReturnsComment()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr8@workflowpro.local", Roles.Manager);
        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK8");

        var createRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Task for Comments", null, TaskPriorities.Normal, null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        var commentRes = await mgrClient.PostAsJsonAsync($"/api/v1/tasks/{created!.Id}/comments",
            new AddTaskCommentRequest("Reviewing the design specs today."));

        Assert.Equal(HttpStatusCode.Created, commentRes.StatusCode);
        var comment = await commentRes.Content.ReadFromJsonAsync<TaskCommentDto>();
        Assert.NotNull(comment);
        Assert.Equal("Reviewing the design specs today.", comment.Body);

        var listRes = await mgrClient.GetAsync($"/api/v1/tasks/{created.Id}/comments");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<TaskCommentDto>>();
        Assert.NotNull(list);
        Assert.Single(list);
    }

    [Fact]
    public async Task ArchivedProject_PreventsModifications()
    {
        var (mgrClient, _, _) = await CreateAuthenticatedClientAsync("task_mgr9@workflowpro.local", Roles.Manager);
        var projectId = await CreateProjectAsync(mgrClient, "PRJ_TSK9");

        var createRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Task in active project", null, TaskPriorities.Normal, null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        // Archive project
        var archiveRes = await mgrClient.PostAsync($"/api/v1/projects/{projectId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveRes.StatusCode);

        // Attempt to create new task in archived project
        var newCreateRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks",
            new CreateTaskRequest("Task in archived project", null, TaskPriorities.Normal, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, newCreateRes.StatusCode);

        // Attempt to update task in archived project
        var updateRes = await mgrClient.PutAsJsonAsync($"/api/v1/tasks/{created!.Id}",
            new UpdateTaskRequest("New Title", null, TaskPriorities.High, null, null, null, Version: 1));
        Assert.Equal(HttpStatusCode.BadRequest, updateRes.StatusCode);
    }
}
