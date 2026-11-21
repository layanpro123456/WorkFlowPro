namespace WorkFlowPro.Application.Features.Auth.Dtos;

public record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles
);
