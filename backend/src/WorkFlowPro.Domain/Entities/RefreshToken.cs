using WorkFlowPro.Domain.Common;

namespace WorkFlowPro.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? DeviceLabel { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { } // For EF Core

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc, string? deviceLabel = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("TokenHash cannot be empty.", nameof(tokenHash));

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        DeviceLabel = deviceLabel;
    }

    public void Revoke(DateTime utcNow, Guid? replacedByTokenId = null)
    {
        if (IsRevoked) return;
        RevokedAtUtc = utcNow;
        ReplacedByTokenId = replacedByTokenId;
        UpdatedAtUtc = utcNow;
    }
}
