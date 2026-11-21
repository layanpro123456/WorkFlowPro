namespace WorkFlowPro.Application.Features.Projects.Dtos;

public record ProjectDetailDto(
    Guid Id,
    string ProjectCode,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    Guid CreatedById,
    string CreatedByName,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<ProjectMemberDto> Members
);
