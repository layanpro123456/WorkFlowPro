namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record CreateTaskRequest(
    string Title,
    string? Description,
    string Priority,
    Guid? AssigneeId,
    DateTime? DueAtUtc,
    int? EstimateMinutes,
    string? Status = null
);
