using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class TicketEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TicketEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task CreateTicket_AsEmployee_Returns201Created_AndInitialStatusIsNew()
    {
        var (requesterClient, _, requesterId) = await CreateAuthenticatedClientAsync("emp_ticket1@workflowpro.local", Roles.Employee);

        var request = new CreateTicketRequest(
            Subject: "Email synchronization failed",
            Description: "Outlook is not pulling new emails from server.",
            Category: TicketCategories.Technical,
            Priority: TicketPriorities.High
        );

        var response = await requesterClient.PostAsJsonAsync("/api/v1/tickets", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TicketSummaryDto>();
        Assert.NotNull(created);
        Assert.True(created.TicketNumber > 0);
        Assert.Equal("Email synchronization failed", created.Subject);
        Assert.Equal(TicketStatuses.New, created.Status);
        Assert.Equal(requesterId, created.RequesterId);
        Assert.Null(created.AssigneeId);

        // Fetch detail and verify initial history
        var detailRes = await requesterClient.GetAsync($"/api/v1/tickets/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = await detailRes.Content.ReadFromJsonAsync<TicketDetailDto>();
        Assert.NotNull(detail);
        Assert.Single(detail.History);
        Assert.Equal(TicketStatuses.New, detail.History[0].ToStatus);
    }

    [Fact]
    public async Task AssignTicket_AsSupportAgent_ToAuthorizedAgent_SucceedsAndTransitionsToAssigned()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket2@workflowpro.local", Roles.Employee);
        var (agent1Client, _, _) = await CreateAuthenticatedClientAsync("agent_ticket2a@workflowpro.local", Roles.SupportAgent);
        var (_, _, agent2Id) = await CreateAuthenticatedClientAsync("agent_ticket2b@workflowpro.local", Roles.SupportAgent);

        // Create ticket
        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Printer offline in Floor 2", "Cannot print invoices", TicketCategories.Hardware, TicketPriorities.Normal));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Agent assigns to Agent 2
        var assignRes = await agent1Client.PutAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/assign",
            new AssignTicketRequest(agent2Id, "Reassigned to hardware team"));
        Assert.Equal(HttpStatusCode.OK, assignRes.StatusCode);

        var updated = await assignRes.Content.ReadFromJsonAsync<TicketSummaryDto>();
        Assert.NotNull(updated);
        Assert.Equal(agent2Id, updated.AssigneeId);
        Assert.Equal(TicketStatuses.Assigned, updated.Status);

        // Verify history has assignment entry
        var historyRes = await agent1Client.GetAsync($"/api/v1/tickets/{ticket.Id}/history");
        var history = await historyRes.Content.ReadFromJsonAsync<List<TicketStatusHistoryDto>>();
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(TicketStatuses.Assigned, history[0].ToStatus);
    }

    [Fact]
    public async Task AssignTicket_ToNonAgentUser_Returns400BadRequest()
    {
        var (requesterClient, _, nonAgentUserId) = await CreateAuthenticatedClientAsync("emp_ticket3@workflowpro.local", Roles.Employee);
        var (agentClient, _, _) = await CreateAuthenticatedClientAsync("agent_ticket3@workflowpro.local", Roles.SupportAgent);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Access Request", "Need access to repo", TicketCategories.Access, TicketPriorities.Low));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Attempt to assign to regular employee
        var assignRes = await agentClient.PutAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/assign",
            new AssignTicketRequest(nonAgentUserId));

        Assert.Equal(HttpStatusCode.BadRequest, assignRes.StatusCode);
        var content = await assignRes.Content.ReadAsStringAsync();
        Assert.Contains("Assignee must be an active support agent, manager, or administrator", content);
    }

    [Fact]
    public async Task AssignTicket_AsOrdinaryEmployee_Returns403Forbidden()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket4@workflowpro.local", Roles.Employee);
        var (_, _, agentId) = await CreateAuthenticatedClientAsync("agent_ticket4@workflowpro.local", Roles.SupportAgent);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Billing query", "Invoice amount incorrect", TicketCategories.Billing, TicketPriorities.Normal));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        var assignRes = await requesterClient.PutAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/assign",
            new AssignTicketRequest(agentId));

        Assert.Equal(HttpStatusCode.Forbidden, assignRes.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_ValidTransitions_AppendsStatusHistory()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket5@workflowpro.local", Roles.Employee);
        var (agentClient, _, agentId) = await CreateAuthenticatedClientAsync("agent_ticket5@workflowpro.local", Roles.SupportAgent);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Bug report in report export", "CSV export corrupts characters", TicketCategories.BugReport, TicketPriorities.High));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Assign
        await agentClient.PutAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/assign", new AssignTicketRequest(agentId));

        // Assigned -> InProgress
        var inProgressRes = await agentClient.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status",
            new ChangeTicketStatusRequest(TicketStatuses.InProgress, "Investigating character encoding"));
        Assert.Equal(HttpStatusCode.OK, inProgressRes.StatusCode);

        // InProgress -> WaitingForRequester
        var waitRes = await agentClient.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status",
            new ChangeTicketStatusRequest(TicketStatuses.WaitingForRequester, "Asked user for sample CSV file"));
        Assert.Equal(HttpStatusCode.OK, waitRes.StatusCode);

        // WaitingForRequester -> InProgress
        var resumeRes = await agentClient.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status",
            new ChangeTicketStatusRequest(TicketStatuses.InProgress, "Received file, fixing bug"));
        Assert.Equal(HttpStatusCode.OK, resumeRes.StatusCode);

        // InProgress -> Resolved
        var resolveRes = await agentClient.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status",
            new ChangeTicketStatusRequest(TicketStatuses.Resolved, "Hotfix deployed to production"));
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        var detail = await agentClient.GetFromJsonAsync<TicketDetailDto>($"/api/v1/tickets/{ticket.Id}");
        Assert.NotNull(detail);
        Assert.Equal(TicketStatuses.Resolved, detail.Status);
        Assert.NotNull(detail.ResolvedAtUtc);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_Returns400BadRequest()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket6@workflowpro.local", Roles.Employee);
        var (agentClient, _, _) = await CreateAuthenticatedClientAsync("agent_ticket6@workflowpro.local", Roles.SupportAgent);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Test Invalid Transition", "Desc", TicketCategories.General, TicketPriorities.Low));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Cannot transition from New directly to Resolved
        var res = await agentClient.PatchAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/status",
            new ChangeTicketStatusRequest(TicketStatuses.Resolved));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AddInternalNote_ByAgent_IsNeverVisibleToRequester_CriticalPrivacyRule()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket7@workflowpro.local", Roles.Employee);
        var (agentClient, _, _) = await CreateAuthenticatedClientAsync("agent_ticket7@workflowpro.local", Roles.SupportAgent);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Sensitive security issue", "Found unusual access pattern", TicketCategories.Technical, TicketPriorities.Urgent));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Public message from agent
        await agentClient.PostAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/messages",
            new AddTicketMessageRequest("We have received your report and are investigating.", IsInternalNote: false));

        // Confidential internal note from agent
        var internalNoteBody = "CONFIDENTIAL_INTERNAL_NOTE: Escalated to CISO and SOC team. User IP logged.";
        var noteRes = await agentClient.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/messages",
            new AddTicketMessageRequest(internalNoteBody, IsInternalNote: true));
        Assert.Equal(HttpStatusCode.Created, noteRes.StatusCode);

        // 1. Verify agent CAN see both messages
        var agentDetail = await agentClient.GetFromJsonAsync<TicketDetailDto>($"/api/v1/tickets/{ticket.Id}");
        Assert.NotNull(agentDetail);
        Assert.Equal(2, agentDetail.Messages.Count);
        Assert.Contains(agentDetail.Messages, m => m.IsInternalNote && m.Body == internalNoteBody);

        var agentMessages = await agentClient.GetFromJsonAsync<List<TicketMessageDto>>($"/api/v1/tickets/{ticket.Id}/messages");
        Assert.NotNull(agentMessages);
        Assert.Equal(2, agentMessages.Count);

        // 2. CRITICAL PRIVACY VERIFICATION: Requester response MUST NEVER include the internal note!
        var requesterDetail = await requesterClient.GetFromJsonAsync<TicketDetailDto>($"/api/v1/tickets/{ticket.Id}");
        Assert.NotNull(requesterDetail);
        Assert.Single(requesterDetail.Messages);
        Assert.False(requesterDetail.Messages[0].IsInternalNote);
        Assert.DoesNotContain(requesterDetail.Messages, m => m.Body.Contains("CONFIDENTIAL_INTERNAL_NOTE"));

        var requesterMessages = await requesterClient.GetFromJsonAsync<List<TicketMessageDto>>($"/api/v1/tickets/{ticket.Id}/messages");
        Assert.NotNull(requesterMessages);
        Assert.Single(requesterMessages);
        Assert.False(requesterMessages[0].IsInternalNote);
        Assert.DoesNotContain(requesterMessages, m => m.Body.Contains("CONFIDENTIAL_INTERNAL_NOTE"));
    }

    [Fact]
    public async Task AddInternalNote_ByRequester_Returns403Forbidden()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket8@workflowpro.local", Roles.Employee);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Account issue", "Desc", TicketCategories.General, TicketPriorities.Low));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Requester tries to set IsInternalNote = true
        var res = await requesterClient.PostAsJsonAsync($"/api/v1/tickets/{ticket!.Id}/messages",
            new AddTicketMessageRequest("Trying to post internal note", IsInternalNote: true));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task GetTicket_ByUnrelatedEmployee_Returns404NotFound()
    {
        var (requesterClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket9a@workflowpro.local", Roles.Employee);
        var (unrelatedClient, _, _) = await CreateAuthenticatedClientAsync("emp_ticket9b@workflowpro.local", Roles.Employee);

        var createRes = await requesterClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            "Confidential Ticket", "Private details", TicketCategories.Billing, TicketPriorities.High));
        var ticket = await createRes.Content.ReadFromJsonAsync<TicketSummaryDto>();

        // Unrelated employee tries to read ticket -> 404 (resource concealment)
        var res = await unrelatedClient.GetAsync($"/api/v1/tickets/{ticket!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
