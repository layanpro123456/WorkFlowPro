using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Infrastructure.Identity;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;

    public JwtTokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SigningKey", "Test_Super_Secret_Key_At_Least_32_Chars_Long!" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpiryMinutes", "15" },
                { "Jwt:RefreshTokenExpiryDays", "7" }
            })
            .Build();

        _service = new JwtTokenService(config);
    }

    [Fact]
    public void GenerateAccessToken_ProducesValidJwtWithExpectedClaims()
    {
        var userId = Guid.NewGuid();
        var email = "test@workflowpro.local";
        var displayName = "Test User";
        var roles = new[] { Roles.Administrator, Roles.Manager };

        var (token, expiresAtUtc) = _service.GenerateAccessToken(userId, email, displayName, roles);

        Assert.NotNull(token);
        Assert.True(expiresAtUtc > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == "TestAudience");
        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(displayName, jwt.Claims.First(c => c.Type == "displayName").Value);

        var tokenRoles = jwt.Claims.Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains(Roles.Administrator, tokenRoles);
        Assert.Contains(Roles.Manager, tokenRoles);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsRawTokenAndMatchingSha256Hash()
    {
        var (rawToken, tokenHash, expiresAtUtc) = _service.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenHash));
        Assert.True(expiresAtUtc > DateTime.UtcNow);

        var computedHash = _service.HashToken(rawToken);
        Assert.Equal(tokenHash, computedHash);
    }
}
