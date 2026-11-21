namespace WorkFlowPro.Application.Features.Auth.Dtos;

public record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserProfileDto User
);
