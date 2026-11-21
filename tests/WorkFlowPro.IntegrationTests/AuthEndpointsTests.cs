using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WorkFlowPro.Application.Features.Auth.Dtos;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_Returns200AndSetsCookie()
    {
        var request = new LoginRequest("admin@workflowpro.local", "Admin@123456");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResult = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken));
        Assert.Equal("admin@workflowpro.local", authResult.User.Email);
        Assert.Equal("System Administrator", authResult.User.DisplayName);
        Assert.Contains("Administrator", authResult.User.Roles);

        Assert.True(response.Headers.Contains("Set-Cookie"));
        var cookies = response.Headers.GetValues("Set-Cookie");
        Assert.Contains(cookies, c => c.Contains("workflowpro_refresh_token") && c.Contains("httponly"));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401Unauthorized()
    {
        var request = new LoginRequest("admin@workflowpro.local", "WrongPassword999!");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var inactiveEmail = $"inactive_{Guid.NewGuid():N}@workflowpro.local";

        var inactiveUser = new ApplicationUser
        {
            UserName = inactiveEmail,
            Email = inactiveEmail,
            DisplayName = "Inactive User",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(inactiveUser, "Password@123");

        var request = new LoginRequest(inactiveEmail, "Password@123");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesTokenAndReturns200()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin@workflowpro.local", "Admin@123456"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var originalAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Post refresh without body (cookie should be carried over by handler)
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(null));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(refreshedAuth);
        Assert.False(string.IsNullOrWhiteSpace(refreshedAuth.AccessToken));
        Assert.NotEqual(originalAuth!.AccessToken, refreshedAuth.AccessToken);
    }

    [Fact]
    public async Task Logout_RevokesSessionAndClearsCookie()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin@workflowpro.local", "Admin@123456"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var logoutResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshTokenRequest(null));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Next refresh attempt must fail because cookie/token is revoked
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(null));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidBearerToken_ReturnsProfile()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin@workflowpro.local", "Admin@123456"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal("admin@workflowpro.local", profile.Email);
        Assert.Contains("Administrator", profile.Roles);
    }
}
