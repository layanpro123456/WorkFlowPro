namespace WorkFlowPro.Application.Features.Projects.Dtos;

public record ProjectSummaryDto(
    Guid Id,
    string ProjectCode,
    string Name,
    string Status,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    int MemberCount,
    DateTime CreatedAtUtc
);
