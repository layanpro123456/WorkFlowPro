namespace WorkFlowPro.Application.Features.Projects.Dtos;

public record ProjectMemberDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string ProjectRole,
    DateTime JoinedAtUtc
);
