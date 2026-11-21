using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class ApprovalEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApprovalEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task CreateWorkflowDefinition_ByManager_Returns201Created()
    {
        var (managerClient, _, _) = await CreateAuthenticatedClientAsync("manager_def@workflowpro.local", Roles.Manager);

        var request = new CreateWorkflowDefinitionRequest(
            "Hardware Purchase Approval",
            "Multi-stage hardware signoff",
            new[]
            {
                new WorkflowStepInputDto(1, "Direct Manager", Roles.Manager, null),
                new WorkflowStepInputDto(2, "IT Administrator", Roles.Administrator, null)
            }
        );

        var res = await managerClient.PostAsJsonAsync("/api/v1/workflow-definitions", request);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var detail = await res.Content.ReadFromJsonAsync<WorkflowDefinitionDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("Hardware Purchase Approval", detail.Name);
        Assert.Equal(2, detail.Steps.Count);
        Assert.Equal(1, detail.Steps[0].StepOrder);
        Assert.Equal(2, detail.Steps[1].StepOrder);
    }

    [Fact]
    public async Task CreateWorkflowDefinition_ByStandardUser_Returns403Forbidden()
    {
        var (employeeClient, _, _) = await CreateAuthenticatedClientAsync("employee_unauth@workflowpro.local", Roles.Employee);

        var request = new CreateWorkflowDefinitionRequest(
            "Unauthorized Def",
            "Desc",
            new[] { new WorkflowStepInputDto(1, "Step 1", Roles.Manager, null) }
        );

        var res = await employeeClient.PostAsJsonAsync("/api/v1/workflow-definitions", request);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ApprovalRequest_EndToEndLifecycle_HappyPath()
    {
        // 1. Setup users
        var (managerClient, _, managerId) = await CreateAuthenticatedClientAsync("mgr_approver@workflowpro.local", Roles.Manager);
        var (adminClient, _, adminId) = await CreateAuthenticatedClientAsync("admin_approver@workflowpro.local", Roles.Administrator);
        var (requesterClient, _, requesterId) = await CreateAuthenticatedClientAsync("requester_user@workflowpro.local", Roles.Employee);

        // 2. Manager creates workflow definition
        var defRequest = new CreateWorkflowDefinitionRequest(
            "Capital Expenditure Flow",
            "Two-step signoff",
            new[]
            {
                new WorkflowStepInputDto(1, "Manager Review", null, managerId),
                new WorkflowStepInputDto(2, "Admin Review", null, adminId)
            }
        );
        var defRes = await managerClient.PostAsJsonAsync("/api/v1/workflow-definitions", defRequest);
        Assert.Equal(HttpStatusCode.Created, defRes.StatusCode);
        var def = await defRes.Content.ReadFromJsonAsync<WorkflowDefinitionDetailDto>();

        // 3. Employee submits approval request
        var submitRequest = new SubmitApprovalRequest(
            def!.Id,
            "Office Ergonomic Chairs",
            "Requesting 5 chairs for team",
            null
        );
        var submitRes = await requesterClient.PostAsJsonAsync("/api/v1/approvals", submitRequest);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var summary = await submitRes.Content.ReadFromJsonAsync<ApprovalRequestSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(ApprovalRequestStatuses.Pending, summary.Status);
        Assert.Equal(1, summary.CurrentStepOrder);
        Assert.Equal(1, summary.Version);

        // 4. Verify wrong approver (requester or admin) trying to decide Step 1 is rejected with 403 Forbidden
        var wrongApproverRes = await requesterClient.PostAsJsonAsync(
            $"/api/v1/approvals/{summary.Id}/decisions",
            new RecordDecisionRequest(ApprovalDecisions.Approved, "Self approval attempt", ExpectedVersion: 1)
        );
        Assert.Equal(HttpStatusCode.Forbidden, wrongApproverRes.StatusCode);

        // 5. Designated Manager approves Step 1
        var step1Res = await managerClient.PostAsJsonAsync(
            $"/api/v1/approvals/{summary.Id}/decisions",
            new RecordDecisionRequest(ApprovalDecisions.Approved, "Manager approved budget", ExpectedVersion: 1)
        );
        Assert.Equal(HttpStatusCode.OK, step1Res.StatusCode);
        var detailAfterStep1 = await step1Res.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>();
        Assert.NotNull(detailAfterStep1);
        Assert.Equal(ApprovalRequestStatuses.Pending, detailAfterStep1.Status);
        Assert.Equal(2, detailAfterStep1.CurrentStepOrder); // Advanced to Step 2
        Assert.Equal(2, detailAfterStep1.Version);

        // 6. Test version conflict on Step 2 (supplying stale version 1 instead of 2)
        var conflictRes = await adminClient.PostAsJsonAsync(
            $"/api/v1/approvals/{summary.Id}/decisions",
            new RecordDecisionRequest(ApprovalDecisions.Approved, "Stale approval", ExpectedVersion: 1)
        );
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);

        // 7. Designated Admin approves Step 2 with correct version 2
        var step2Res = await adminClient.PostAsJsonAsync(
            $"/api/v1/approvals/{summary.Id}/decisions",
            new RecordDecisionRequest(ApprovalDecisions.Approved, "Final signoff granted", ExpectedVersion: 2)
        );
        Assert.Equal(HttpStatusCode.OK, step2Res.StatusCode);
        var finalDetail = await step2Res.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>();
        Assert.NotNull(finalDetail);
        Assert.Equal(ApprovalRequestStatuses.Approved, finalDetail.Status);
        Assert.Null(finalDetail.CurrentStepOrder); // Completed
        Assert.NotNull(finalDetail.CompletedAtUtc);
        Assert.Equal(3, finalDetail.Version);
        Assert.All(finalDetail.Steps, s => Assert.Equal(ApprovalStepStatuses.Approved, s.Status));
    }

    [Fact]
    public async Task ApprovalRequest_Rejection_TerminatesRequestAndSkipsRemainingSteps()
    {
        var (managerClient, _, managerId) = await CreateAuthenticatedClientAsync("mgr_rejector@workflowpro.local", Roles.Manager);
        var (adminClient, _, adminId) = await CreateAuthenticatedClientAsync("admin_skip@workflowpro.local", Roles.Administrator);
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("requester_reject@workflowpro.local", Roles.Employee);

        var defRequest = new CreateWorkflowDefinitionRequest(
            "Cloud Subscription Flow",
            "Desc",
            new[]
            {
                new WorkflowStepInputDto(1, "Manager Review", null, managerId),
                new WorkflowStepInputDto(2, "Admin Review", null, adminId)
            }
        );
        var defRes = await managerClient.PostAsJsonAsync("/api/v1/workflow-definitions", defRequest);
        var def = await defRes.Content.ReadFromJsonAsync<WorkflowDefinitionDetailDto>();

        var submitRes = await requesterClient.PostAsJsonAsync(
            "/api/v1/approvals",
            new SubmitApprovalRequest(def!.Id, "Expensive SaaS Tool", "Annual enterprise license", null)
        );
        var summary = await submitRes.Content.ReadFromJsonAsync<ApprovalRequestSummaryDto>();

        // Manager rejects step 1
        var rejectRes = await managerClient.PostAsJsonAsync(
            $"/api/v1/approvals/{summary!.Id}/decisions",
            new RecordDecisionRequest(ApprovalDecisions.Rejected, "Out of budget this quarter", ExpectedVersion: 1)
        );
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);

        var detail = await rejectRes.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(ApprovalRequestStatuses.Rejected, detail.Status);
        Assert.Null(detail.CurrentStepOrder);
        Assert.NotNull(detail.CompletedAtUtc);

        var step1 = detail.Steps.First(s => s.StepOrder == 1);
        Assert.Equal(ApprovalStepStatuses.Rejected, step1.Status);
        Assert.Equal("Out of budget this quarter", step1.Decision?.Comment);

        var step2 = detail.Steps.First(s => s.StepOrder == 2);
        Assert.Equal(ApprovalStepStatuses.Skipped, step2.Status);
        Assert.Null(step2.DecidedAtUtc);
    }

    [Fact]
    public async Task CancelRequest_ByRequester_CancelsPendingRequest()
    {
        var (managerClient, _, managerId) = await CreateAuthenticatedClientAsync("mgr_cancel@workflowpro.local", Roles.Manager);
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("requester_canceller@workflowpro.local", Roles.Employee);

        var defRequest = new CreateWorkflowDefinitionRequest(
            "Office Relocation Flow",
            "Desc",
            new[] { new WorkflowStepInputDto(1, "Manager Review", null, managerId) }
        );
        var defRes = await managerClient.PostAsJsonAsync("/api/v1/workflow-definitions", defRequest);
        var def = await defRes.Content.ReadFromJsonAsync<WorkflowDefinitionDetailDto>();

        var submitRes = await requesterClient.PostAsJsonAsync(
            "/api/v1/approvals",
            new SubmitApprovalRequest(def!.Id, "Desk Shift", "Move to 4th floor", null)
        );
        var summary = await submitRes.Content.ReadFromJsonAsync<ApprovalRequestSummaryDto>();

        // Requester cancels
        var cancelRes = await requesterClient.PostAsJsonAsync(
            $"/api/v1/approvals/{summary!.Id}/cancel",
            new CancelApprovalRequest(ExpectedVersion: 1)
        );
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var detail = await cancelRes.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(ApprovalRequestStatuses.Cancelled, detail.Status);
        Assert.All(detail.Steps, s => Assert.Equal(ApprovalStepStatuses.Skipped, s.Status));
    }

    [Fact]
    public async Task GetById_UnrelatedUser_Returns404NotFound_ConcealingResource()
    {
        var (managerClient, _, managerId) = await CreateAuthenticatedClientAsync("mgr_private@workflowpro.local", Roles.Manager);
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("requester_private@workflowpro.local", Roles.Employee);
        var (outsiderClient, _, _) = await CreateAuthenticatedClientAsync("outsider_user@workflowpro.local", Roles.Employee);

        var defRequest = new CreateWorkflowDefinitionRequest(
            "Confidential IP Flow",
            "Desc",
            new[] { new WorkflowStepInputDto(1, "Manager Review", null, managerId) }
        );
        var defRes = await managerClient.PostAsJsonAsync("/api/v1/workflow-definitions", defRequest);
        var def = await defRes.Content.ReadFromJsonAsync<WorkflowDefinitionDetailDto>();

        var submitRes = await requesterClient.PostAsJsonAsync(
            "/api/v1/approvals",
            new SubmitApprovalRequest(def!.Id, "Patent Filing", "Confidential disclosure", null)
        );
        var summary = await submitRes.Content.ReadFromJsonAsync<ApprovalRequestSummaryDto>();

        // Outsider attempts to view private approval request
        var outsiderRes = await outsiderClient.GetAsync($"/api/v1/approvals/{summary!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, outsiderRes.StatusCode);
    }
}
