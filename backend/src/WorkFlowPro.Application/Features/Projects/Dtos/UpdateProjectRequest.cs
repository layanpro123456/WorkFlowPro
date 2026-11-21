namespace WorkFlowPro.Application.Features.Projects.Dtos;

public record UpdateProjectRequest(
    string Name,
    string? Description,
    string? Status,
    DateOnly? StartDate,
    DateOnly? TargetDate
);
