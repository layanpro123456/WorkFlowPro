namespace WorkFlowPro.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) GenerateAccessToken(
        Guid userId,
        string email,
        string displayName,
        IEnumerable<string> roles);

    (string RawToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken();

    string HashToken(string rawToken);
}
