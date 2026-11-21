using WorkFlowPro.Application.Features.Auth.Dtos;

namespace WorkFlowPro.Application.Features.Auth.Services;

public interface IAuthAppService
{
    Task<(AuthResponseDto Response, string RawRefreshToken)> LoginAsync(
        LoginRequest request,
        string? deviceLabel,
        CancellationToken cancellationToken);

    Task<(AuthResponseDto Response, string NewRawRefreshToken)> RefreshTokenAsync(
        string rawRefreshToken,
        string? deviceLabel,
        CancellationToken cancellationToken);

    Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken);

    Task<UserProfileDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}
