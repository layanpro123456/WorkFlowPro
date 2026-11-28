using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Application.Features.Sprints.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class AgileEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AgileEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedClientAsync(string email, string role)
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

        var loginClient = _factory.CreateClient();
        var loginRes = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Password@123"));
        loginRes.EnsureSuccessStatusCode();
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, user.Id);
    }

    [Fact]
    public async Task EndToEnd_AgileEngine_Sprints_WipLimits_Hierarchy_Links_And_Velocity()
    {
        var (mgrClient, _) = await CreateAuthenticatedClientAsync("agile_mgr@workflowpro.local", Roles.Manager);
        var (devClient, devId) = await CreateAuthenticatedClientAsync("agile_dev@workflowpro.local", Roles.Employee);

        var projRes = await mgrClient.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("AGILE", "Agile Platform", "Scrum & Kanban", null, null));
        projRes.EnsureSuccessStatusCode();
        var project = await projRes.Content.ReadFromJsonAsync<ProjectSummaryDto>();

        await mgrClient.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/members",
            new AddProjectMemberRequest(devId, ProjectRoles.Member));

        // 1. Create Epic, two Stories, and a Subtask
        var epicRes = await mgrClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Core Auth Epic", null, TaskPriorities.High, null, null, null, TaskStatuses.Backlog, IssueTypes.Epic, 13));
        epicRes.EnsureSuccessStatusCode();
        var epic = await epicRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        var story1Res = await devClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Story 1 Login", null, TaskPriorities.High, devId, null, null, TaskStatuses.ToDo, IssueTypes.Story, 5, epic!.Id));
        story1Res.EnsureSuccessStatusCode();
        var story1 = await story1Res.Content.ReadFromJsonAsync<TaskSummaryDto>();

        var story2Res = await devClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Story 2 Logout", null, TaskPriorities.Normal, devId, null, null, TaskStatuses.ToDo, IssueTypes.Story, 3, epic.Id));
        story2Res.EnsureSuccessStatusCode();
        var story2 = await story2Res.Content.ReadFromJsonAsync<TaskSummaryDto>();

        var subtaskRes = await devClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Subtask Token Check", null, TaskPriorities.Normal, devId, null, null, TaskStatuses.ToDo, IssueTypes.Subtask, 1, epic.Id, story1!.Id));
        subtaskRes.EnsureSuccessStatusCode();
        var subtask = await subtaskRes.Content.ReadFromJsonAsync<TaskSummaryDto>();

        // Nested Subtask under Subtask must fail
        var badSubtaskRes = await devClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Nested Subtask", null, TaskPriorities.Normal, null, null, null, TaskStatuses.ToDo, IssueTypes.Subtask, null, null, subtask!.Id));
        Assert.Equal(HttpStatusCode.BadRequest, badSubtaskRes.StatusCode);

        // 2. Create two Sprints, start Sprint 1, verify second active sprint fails
        var sp1 = await (await mgrClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/sprints",
            new CreateSprintRequest("Sprint 1", "Ship Auth"))).Content.ReadFromJsonAsync<SprintDto>();
        var sp2 = await (await mgrClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/sprints",
            new CreateSprintRequest("Sprint 2", "Next Iteration"))).Content.ReadFromJsonAsync<SprintDto>();

        var start1Res = await mgrClient.PostAsJsonAsync($"/api/v1/sprints/{sp1!.Id}/start",
            new StartSprintRequest(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(12)));
        start1Res.EnsureSuccessStatusCode();

        var start2Res = await mgrClient.PostAsJsonAsync($"/api/v1/sprints/{sp2!.Id}/start",
            new StartSprintRequest(DateTime.UtcNow, DateTime.UtcNow.AddDays(14)));
        Assert.Equal(HttpStatusCode.BadRequest, start2Res.StatusCode);

        // Assign Story 1 & Story 2 to Sprint 1
        (await devClient.PatchAsJsonAsync($"/api/v1/tasks/{story1.Id}/sprint", new AssignTaskSprintRequest(sp1.Id, 100))).EnsureSuccessStatusCode();
        (await devClient.PatchAsJsonAsync($"/api/v1/tasks/{story2!.Id}/sprint", new AssignTaskSprintRequest(sp1.Id, 200))).EnsureSuccessStatusCode();

        // 3. Configure strict WIP limit = 1 on InProgress
        var wipUpdateRes = await mgrClient.PutAsJsonAsync($"/api/v1/projects/{project.Id}/board-config",
            new UpdateBoardColumnConfigsRequest(new[]
            {
                new BoardColumnConfigDto(TaskStatuses.InProgress, 2, WipLimit: 1, IsStrictWip: true, IsVisible: true)
            }));
        wipUpdateRes.EnsureSuccessStatusCode();

        // Move Story 1 -> InProgress succeeds; Story 2 -> InProgress hits WIP limit 400
        var move1Res = await devClient.PatchAsJsonAsync($"/api/v1/tasks/{story1.Id}/move",
            new MoveTaskOnBoardRequest(TaskStatuses.InProgress, 100, story1.Version));
        move1Res.EnsureSuccessStatusCode();
        story1 = await move1Res.Content.ReadFromJsonAsync<TaskSummaryDto>();

        var move2Blocked = await devClient.PatchAsJsonAsync($"/api/v1/tasks/{story2.Id}/move",
            new MoveTaskOnBoardRequest(TaskStatuses.InProgress, 200, story2.Version));
        Assert.Equal(HttpStatusCode.BadRequest, move2Blocked.StatusCode);

        // Complete Story 1 -> Done
        var done1Res = await devClient.PatchAsJsonAsync($"/api/v1/tasks/{story1!.Id}/move",
            new MoveTaskOnBoardRequest(TaskStatuses.Done, 100, story1.Version));
        done1Res.EnsureSuccessStatusCode();

        // 4. Issue Links + Circular Blocking Detection
        var linkRes = await devClient.PostAsJsonAsync($"/api/v1/tasks/{story2.Id}/links",
            new CreateIssueLinkRequest(story1.Id, IssueLinkTypes.Blocks));
        Assert.Equal(HttpStatusCode.Created, linkRes.StatusCode);
        var createdLink = await linkRes.Content.ReadFromJsonAsync<IssueLinkDto>();

        var cycleRes = await devClient.PostAsJsonAsync($"/api/v1/tasks/{story1.Id}/links",
            new CreateIssueLinkRequest(story2.Id, IssueLinkTypes.Blocks));
        Assert.Equal(HttpStatusCode.BadRequest, cycleRes.StatusCode);

        var delLinkRes = await devClient.DeleteAsync($"/api/v1/tasks/{story2.Id}/links/{createdLink!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delLinkRes.StatusCode);

        // 5. Complete Sprint 1 with rollover of incomplete Story 2 -> Sprint 2
        var completeRes = await mgrClient.PostAsJsonAsync($"/api/v1/sprints/{sp1.Id}/complete",
            new CompleteSprintRequest(MoveIncompleteToSprintId: sp2.Id));
        completeRes.EnsureSuccessStatusCode();
        var completeDto = await completeRes.Content.ReadFromJsonAsync<CompleteSprintResultDto>();
        Assert.Equal(1, completeDto!.CompletedTasksCount);
        Assert.Equal(1, completeDto.RolledOverTasksCount);
        Assert.Equal(sp2.Id, completeDto.RolledOverToSprintId);

        // Verify Story 2 is now in Sprint 2
        var story2Detail = await (await devClient.GetAsync($"/api/v1/tasks/{story2.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        Assert.Equal(sp2.Id, story2Detail!.SprintId);

        // 6. Verify Burndown & Velocity
        var burndown = await (await devClient.GetAsync($"/api/v1/sprints/{sp1.Id}/burndown")).Content.ReadFromJsonAsync<SprintBurndownDto>();
        Assert.NotNull(burndown);
        Assert.True(burndown!.Points.Count >= 2);

        var velocity = await (await devClient.GetAsync($"/api/v1/projects/{project.Id}/velocity")).Content.ReadFromJsonAsync<VelocityChartDto>();
        Assert.NotNull(velocity);
        Assert.Single(velocity!.Sprints);
        Assert.Equal(5, velocity.Sprints[0].CompletedPoints);
    }
}
