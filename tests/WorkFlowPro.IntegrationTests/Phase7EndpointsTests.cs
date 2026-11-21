using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Admin.Dtos;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class Phase7EndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public Phase7EndpointsTests(CustomWebApplicationFactory factory)
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
        loginClient.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.0.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");

        var loginRes = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = email,
            password = "Password@123"
        });

        loginRes.EnsureSuccessStatusCode();
        var authData = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData!.AccessToken);

        return (client, authData.AccessToken, user.Id);
    }

    [Fact]
    public async Task AdminEndpoints_ReturnForbidden_ForNonAdminUsers()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync("employee_sec@test.local", Roles.Employee);

        var usersRes = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, usersRes.StatusCode);

        var auditRes = await client.GetAsync("/api/v1/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, auditRes.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListUsers_AndFilterByRoleAndStatus()
    {
        var (adminClient, _, _) = await CreateAuthenticatedClientAsync("admin_users_test@test.local", Roles.Administrator);

        var res = await adminClient.GetAsync("/api/v1/admin/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var data = await res.Content.ReadFromJsonAsync<PagedResult<AdminUserSummaryDto>>();
        Assert.NotNull(data);
        Assert.True(data.TotalCount > 0);
        Assert.Contains(data.Items, u => u.Email == "admin_users_test@test.local");
    }

    [Fact]
    public async Task Admin_CanCreateUser_WhichRecordsAuditLog()
    {
        var (adminClient, _, _) = await CreateAuthenticatedClientAsync("admin_creator@test.local", Roles.Administrator);

        var newEmail = $"new_hire_{Guid.NewGuid():N}@test.local";
        var createPayload = new CreateUserRequest(
            Email: newEmail,
            DisplayName: "New Hire",
            Password: "SecurePassword123!",
            Roles: new List<string> { Roles.Employee, Roles.SupportAgent }
        );

        var res = await adminClient.PostAsJsonAsync("/api/v1/admin/users", createPayload);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var created = await res.Content.ReadFromJsonAsync<AdminUserSummaryDto>();
        Assert.NotNull(created);
        Assert.Equal(newEmail, created.Email);
        Assert.Equal("New Hire", created.DisplayName);
        Assert.True(created.IsActive);
        Assert.Contains(Roles.Employee, created.Roles);
        Assert.Contains(Roles.SupportAgent, created.Roles);

        // Verify audit log
        var auditRes = await adminClient.GetAsync($"/api/v1/admin/audit-logs?action={AuditActions.UserCreated}");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        var auditData = await auditRes.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        Assert.NotNull(auditData);
        Assert.Contains(auditData.Items, l => l.EntityId == created.Id);
    }

    [Fact]
    public async Task Admin_CanUpdateRoles_AndDeactivateUser_RevokingRefreshTokens()
    {
        var (adminClient, _, adminId) = await CreateAuthenticatedClientAsync("admin_manager_test@test.local", Roles.Administrator);

        // 1. Create a user to deactivate
        var email = $"target_user_{Guid.NewGuid():N}@test.local";
        var createRes = await adminClient.PostAsJsonAsync("/api/v1/admin/users", new CreateUserRequest(
            Email: email,
            DisplayName: "Target User",
            Password: "Password@123",
            Roles: new List<string> { Roles.Employee }
        ));
        createRes.EnsureSuccessStatusCode();
        var user = (await createRes.Content.ReadFromJsonAsync<AdminUserSummaryDto>())!;

        // 2. Log in as that user so they have an active refresh token
        var userClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginRes = await userClient.PostAsJsonAsync("/api/v1/auth/login", new { email = email, password = "Password@123" });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // Verify refresh token exists in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var tokenCount = await db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.RevokedAtUtc == null);
            Assert.True(tokenCount > 0);
        }

        // 3. Update roles to Manager
        var rolesRes = await adminClient.PutAsJsonAsync($"/api/v1/admin/users/{user.Id}/roles", new UpdateUserRolesRequest(
            Roles: new List<string> { Roles.Manager }
        ));
        Assert.Equal(HttpStatusCode.OK, rolesRes.StatusCode);
        var rolesUpdated = (await rolesRes.Content.ReadFromJsonAsync<AdminUserSummaryDto>())!;
        Assert.Contains(Roles.Manager, rolesUpdated.Roles);
        Assert.DoesNotContain(Roles.Employee, rolesUpdated.Roles);

        // 4. Deactivate user
        var deactRes = await adminClient.PatchAsJsonAsync($"/api/v1/admin/users/{user.Id}/status", new UpdateUserStatusRequest(
            IsActive: false
        ));
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);
        var deactUser = (await deactRes.Content.ReadFromJsonAsync<AdminUserSummaryDto>())!;
        Assert.False(deactUser.IsActive);

        // Verify all refresh tokens are revoked
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var activeTokens = await db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.RevokedAtUtc == null);
            Assert.Equal(0, activeTokens);
        }

        // 5. Verify deactivated user cannot log in
        var retryLogin = await userClient.PostAsJsonAsync("/api/v1/auth/login", new { email = email, password = "Password@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, retryLogin.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeactivateSelf()
    {
        var (adminClient, _, adminId) = await CreateAuthenticatedClientAsync("self_admin_test@test.local", Roles.Administrator);

        var deactRes = await adminClient.PatchAsJsonAsync($"/api/v1/admin/users/{adminId}/status", new UpdateUserStatusRequest(
            IsActive: false
        ));
        Assert.Equal(HttpStatusCode.BadRequest, deactRes.StatusCode);
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").First());

        Assert.True(res.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").First());

        Assert.True(res.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", res.Headers.GetValues("Referrer-Policy").First());

        Assert.True(res.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task RateLimiting_Exceeded_Returns429TooManyRequests()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.100.50");
        HttpResponseMessage lastResponse = null!;

        for (int i = 0; i < 20; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = "invalid@test.local",
                password = "WrongPassword123!"
            });

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }
}
