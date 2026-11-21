using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Dashboard.Dtos;
using WorkFlowPro.Application.Features.Notifications.Dtos;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Application.Features.Reports.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class Phase6EndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public Phase6EndpointsTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task Notification_Isolation_UserCannotAccessOthersNotification()
    {
        var (userAClient, _, userAId) = await CreateAuthenticatedClientAsync("user_notif_a@workflowpro.local", Roles.Employee);
        var (userBClient, _, userBId) = await CreateAuthenticatedClientAsync("user_notif_b@workflowpro.local", Roles.Employee);
        var (managerClient, _, _) = await CreateAuthenticatedClientAsync("mgr_notif_setup@workflowpro.local", Roles.Manager);

        // Manager creates project and assigns task to User A
        var projRes = await managerClient.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(
            "NOTIF", "Notif Project", "Testing notifications", DateOnly.FromDateTime(DateTime.Today), null
        ));
        var proj = await projRes.Content.ReadFromJsonAsync<ProjectDetailDto>();

        await managerClient.PostAsJsonAsync($"/api/v1/projects/{proj!.Id}/members", new AddProjectMemberRequest(userAId, ProjectRoles.Member));

        // Create task assigning to User A
        await managerClient.PostAsJsonAsync($"/api/v1/projects/{proj.Id}/tasks", new CreateTaskRequest(
            "Setup telemetry", "Telemetry configuration", "High", userAId, null, null, "ToDo"
        ));

        // User A gets notification
        var userANotifsRes = await userAClient.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.OK, userANotifsRes.StatusCode);
        var userANotifs = await userANotifsRes.Content.ReadFromJsonAsync<NotificationListResult>();
        Assert.NotNull(userANotifs);
        Assert.NotEmpty(userANotifs.Items);
        var notifA = userANotifs.Items.First();

        // User B cannot see User A's notification
        var userBNotifsRes = await userBClient.GetAsync("/api/v1/notifications");
        var userBNotifs = await userBNotifsRes.Content.ReadFromJsonAsync<NotificationListResult>();
        Assert.DoesNotContain(userBNotifs!.Items, n => n.Id == notifA.Id);

        // User B attempting to mark User A's notification as read receives 404 Not Found
        var patchRes = await userBClient.PatchAsync($"/api/v1/notifications/{notifA.Id}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, patchRes.StatusCode);

        // User A can mark their own notification as read
        var readRes = await userAClient.PatchAsync($"/api/v1/notifications/{notifA.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, readRes.StatusCode);
    }

    [Fact]
    public async Task ApprovalSubmission_CreatesInAppNotification_ForCurrentApprover()
    {
        var (approverClient, _, approverId) = await CreateAuthenticatedClientAsync("approver_notif@workflowpro.local", Roles.Manager);
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("req_notif@workflowpro.local", Roles.Employee);

        var defRes = await approverClient.PostAsJsonAsync("/api/v1/workflow-definitions", new CreateWorkflowDefinitionRequest(
            "Notification Test Flow",
            "Single step",
            new[] { new WorkflowStepInputDto(1, "Direct Approval", null, approverId) }
        ));
        var def = await defRes.Content.ReadFromJsonAsync<WorkflowDefinitionDetailDto>();

        // Requester submits request
        var submitRes = await requesterClient.PostAsJsonAsync("/api/v1/approvals", new SubmitApprovalRequest(
            def!.Id, "Conference Ticket", "Purchase ticket", null
        ));
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);

        // Approver checks notifications
        var notifsRes = await approverClient.GetAsync("/api/v1/notifications");
        var notifs = await notifsRes.Content.ReadFromJsonAsync<NotificationListResult>();
        Assert.NotNull(notifs);
        Assert.Contains(notifs.Items, n => n.Type == NotificationTypes.ApprovalActionRequired && n.Message.Contains("Conference Ticket"));
    }

    [Fact]
    public async Task Dashboard_Summary_ReflectsAuthorizedPersistedData()
    {
        var (client, _, userId) = await CreateAuthenticatedClientAsync("dash_user@workflowpro.local", Roles.Employee);

        var summaryRes = await client.GetAsync("/api/v1/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);

        var summary = await summaryRes.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        Assert.NotNull(summary);
        Assert.True(summary.AssignedTasksCount >= 0);
        Assert.True(summary.OpenTicketsCount >= 0);
        Assert.True(summary.PendingApprovalsCount >= 0);
    }

    [Fact]
    public async Task Reports_Tasks_AuthorizationAndCsvExport()
    {
        var (managerClient, _, managerId) = await CreateAuthenticatedClientAsync("rep_mgr@workflowpro.local", Roles.Manager);
        var (outsiderClient, _, _) = await CreateAuthenticatedClientAsync("rep_outsider@workflowpro.local", Roles.Employee);

        var projRes = await managerClient.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(
            "REP1", "Report Project", "Reports test", DateOnly.FromDateTime(DateTime.Today), null
        ));
        var proj = await projRes.Content.ReadFromJsonAsync<ProjectDetailDto>();

        // Create tasks
        await managerClient.PostAsJsonAsync($"/api/v1/projects/{proj!.Id}/tasks", new CreateTaskRequest(
            "Task 1", "Description 1", "Normal", managerId, null, null, "Done"
        ));
        await managerClient.PostAsJsonAsync($"/api/v1/projects/{proj.Id}/tasks", new CreateTaskRequest(
            "Task 2", "Description 2", "High", managerId, null, null, "InProgress"
        ));

        // Manager queries task report
        var reportRes = await managerClient.GetAsync($"/api/v1/reports/tasks?projectId={proj.Id}");
        Assert.Equal(HttpStatusCode.OK, reportRes.StatusCode);

        var report = await reportRes.Content.ReadFromJsonAsync<TaskReportDto>();
        Assert.NotNull(report);
        Assert.Equal(2, report.TotalTasks);
        Assert.Equal(1, report.CompletedTasks);
        Assert.Equal(50.0, report.CompletionRate);

        // Outsider receives 404 Not Found for unauthorized project
        var outsiderRes = await outsiderClient.GetAsync($"/api/v1/reports/tasks?projectId={proj.Id}");
        Assert.Equal(HttpStatusCode.NotFound, outsiderRes.StatusCode);

        // Manager exports CSV
        var csvRes = await managerClient.GetAsync($"/api/v1/reports/tasks?projectId={proj.Id}&exportCsv=true");
        Assert.Equal(HttpStatusCode.OK, csvRes.StatusCode);
        Assert.Equal("text/csv", csvRes.Content.Headers.ContentType?.MediaType);

        var csvContent = await csvRes.Content.ReadAsStringAsync();
        Assert.Contains("TaskId,ProjectCode,ProjectName,Title,Status,Priority,Assignee,DueDateUtc,CreatedAtUtc", csvContent);
        Assert.Contains("Task 1", csvContent);
        Assert.Contains("Task 2", csvContent);
    }

    [Fact]
    public async Task Reports_Tickets_ExportCsv()
    {
        var (agentClient, _, _) = await CreateAuthenticatedClientAsync("rep_agent@workflowpro.local", Roles.SupportAgent);
        var (reqClient, _, _) = await CreateAuthenticatedClientAsync("rep_req@workflowpro.local", Roles.Employee);

        await reqClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Printer Offline", "Floor 2 printer jammed", "Hardware", "High", null
        ));

        var csvRes = await agentClient.GetAsync("/api/v1/reports/tickets?exportCsv=true");
        Assert.Equal(HttpStatusCode.OK, csvRes.StatusCode);
        Assert.Equal("text/csv", csvRes.Content.Headers.ContentType?.MediaType);

        var csv = await csvRes.Content.ReadAsStringAsync();
        Assert.Contains("TicketNumber,Subject,Category,Priority,Status,Requester,Assignee,CreatedAtUtc,ResolvedAtUtc", csv);
        Assert.Contains("Printer Offline", csv);
    }
}
