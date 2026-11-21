using WorkFlowPro.Domain.Entities;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class RefreshTokenTests
{
    [Fact]
    public void RefreshToken_WhenCreated_IsActiveAndNotExpired()
    {
        var userId = Guid.NewGuid();
        var tokenHash = "dummyhash1234567890";
        var expiresAtUtc = DateTime.UtcNow.AddDays(7);

        var token = new RefreshToken(userId, tokenHash, expiresAtUtc, "Chrome on Windows");

        Assert.Equal(userId, token.UserId);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.Equal(expiresAtUtc, token.ExpiresAtUtc);
        Assert.Equal("Chrome on Windows", token.DeviceLabel);
        Assert.True(token.IsActive);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired);
    }

    [Fact]
    public void RefreshToken_WhenRevoked_IsActiveBecomesFalse()
    {
        var userId = Guid.NewGuid();
        var token = new RefreshToken(userId, "hash123", DateTime.UtcNow.AddDays(7));
        var replacedById = Guid.NewGuid();
        var now = DateTime.UtcNow;

        token.Revoke(now, replacedById);

        Assert.False(token.IsActive);
        Assert.True(token.IsRevoked);
        Assert.Equal(now, token.RevokedAtUtc);
        Assert.Equal(replacedById, token.ReplacedByTokenId);
    }

    [Fact]
    public void RefreshToken_WhenPastExpiry_IsExpiredIsTrue()
    {
        var userId = Guid.NewGuid();
        var pastExpiry = DateTime.UtcNow.AddMinutes(-5);
        var token = new RefreshToken(userId, "hash123", pastExpiry);

        Assert.True(token.IsExpired);
        Assert.False(token.IsActive);
    }

    [Fact]
    public void RefreshToken_WithInvalidArguments_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.Empty, "validhash", DateTime.UtcNow.AddDays(1)));
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.NewGuid(), "", DateTime.UtcNow.AddDays(1)));
    }
}
