namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record UpdateTaskRequest(
    string Title,
    string? Description,
    string Priority,
    Guid? AssigneeId,
    DateTime? DueAtUtc,
    int? EstimateMinutes,
    int Version
);
