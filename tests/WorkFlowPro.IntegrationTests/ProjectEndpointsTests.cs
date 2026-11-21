using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class ProjectEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task CreateProject_WithValidInput_Returns201AndSetsOwner()
    {
        var (client, _, userId) = await CreateAuthenticatedClientAsync("manager1@workflowpro.local", Roles.Manager);

        var code = $"PRJ_{Guid.NewGuid():N}"[..12];
        var request = new CreateProjectRequest(code, "New Project", "Description", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        var response = await client.PostAsJsonAsync("/api/v1/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProjectSummaryDto>();
        Assert.NotNull(created);
        Assert.Equal(code.ToUpperInvariant(), created.ProjectCode);
        Assert.Equal("New Project", created.Name);
        Assert.Equal(ProjectStatuses.Active, created.Status);
        Assert.Equal(1, created.MemberCount);

        // Fetch detail to verify owner membership
        var detailRes = await client.GetAsync($"/api/v1/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = await detailRes.Content.ReadFromJsonAsync<ProjectDetailDto>();
        Assert.NotNull(detail);
        Assert.Single(detail.Members);
        Assert.Equal(userId, detail.Members[0].UserId);
        Assert.Equal(ProjectRoles.Owner, detail.Members[0].ProjectRole);
    }

    [Fact]
    public async Task CreateProject_WithDuplicateCode_Returns400BadRequest()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync("manager_dup@workflowpro.local", Roles.Manager);
        var code = $"DUP_{Guid.NewGuid():N}"[..12];

        var res1 = await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(code, "Project 1", null, null, null));
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(code, "Project 2", null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task NonMember_CannotReadRestrictedProject()
    {
        var (creatorClient, _, _) = await CreateAuthenticatedClientAsync("creator@workflowpro.local", Roles.Manager);
        var (nonMemberClient, _, _) = await CreateAuthenticatedClientAsync("nonmember@workflowpro.local", Roles.Employee);

        var code = $"SEC_{Guid.NewGuid():N}"[..12];
        var createRes = await creatorClient.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(code, "Confidential", null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<ProjectSummaryDto>();

        // Non-member attempts to read project detail
        var readRes = await nonMemberClient.GetAsync($"/api/v1/projects/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, readRes.StatusCode);
    }

    [Fact]
    public async Task Administrator_CanReadAnyProject()
    {
        var (creatorClient, _, _) = await CreateAuthenticatedClientAsync("creator2@workflowpro.local", Roles.Manager);
        var (adminClient, _, _) = await CreateAuthenticatedClientAsync("admin_super@workflowpro.local", Roles.Administrator);

        var code = $"ADM_{Guid.NewGuid():N}"[..12];
        var createRes = await creatorClient.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(code, "Admin Visible", null, null, null));
        var created = await createRes.Content.ReadFromJsonAsync<ProjectSummaryDto>();

        var readRes = await adminClient.GetAsync($"/api/v1/projects/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_And_Archive_WorksAsExpected()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync("manager_upd@workflowpro.local", Roles.Manager);
        var code = $"UPD_{Guid.NewGuid():N}"[..12];

        var createRes = await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(code, "Initial Name", "Desc", null, null));
        var created = await createRes.Content.ReadFromJsonAsync<ProjectSummaryDto>();

        // Update
        var updateRequest = new UpdateProjectRequest("Updated Name", "Updated Desc", ProjectStatuses.OnHold, null, null);
        var updateRes = await client.PutAsJsonAsync($"/api/v1/projects/{created!.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<ProjectDetailDto>();
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(ProjectStatuses.OnHold, updated.Status);

        // Archive
        var archiveRes = await client.PostAsync($"/api/v1/projects/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveRes.StatusCode);

        // Updating archived project should fail
        var failedUpdate = await client.PutAsJsonAsync($"/api/v1/projects/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.BadRequest, failedUpdate.StatusCode);
    }

    [Fact]
    public async Task AddAndRemoveMember_WorksAndEnforcesOwnerInvariant()
    {
        var (ownerClient, _, ownerId) = await CreateAuthenticatedClientAsync("manager_mem@workflowpro.local", Roles.Manager);
        var (_, _, memberUserId) = await CreateAuthenticatedClientAsync("user_to_add@workflowpro.local", Roles.Employee);

        var code = $"MEM_{Guid.NewGuid():N}"[..12];
        var createRes = await ownerClient.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(code, "Membership Test", null, null, null));
        var project = await createRes.Content.ReadFromJsonAsync<ProjectSummaryDto>();

        // Add member
        var addRes = await ownerClient.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/members", new AddProjectMemberRequest(memberUserId, ProjectRoles.Member));
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);

        // Duplicate add must fail
        var duplicateAdd = await ownerClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members", new AddProjectMemberRequest(memberUserId, ProjectRoles.Member));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateAdd.StatusCode);

        // Remove the newly added member
        var removeRes = await ownerClient.DeleteAsync($"/api/v1/projects/{project.Id}/members/{memberUserId}");
        Assert.Equal(HttpStatusCode.NoContent, removeRes.StatusCode);

        // Attempting to remove the only owner must fail
        var removeOwnerRes = await ownerClient.DeleteAsync($"/api/v1/projects/{project.Id}/members/{ownerId}");
        Assert.Equal(HttpStatusCode.BadRequest, removeOwnerRes.StatusCode);
    }
}
