namespace WorkFlowPro.Application.Features.Projects.Dtos;

public record CreateProjectRequest(
    string ProjectCode,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? TargetDate
);
