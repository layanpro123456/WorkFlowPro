namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record UpdateTaskRequest(
    string Title,
    string? Description,
    string Priority,
    Guid? AssigneeId,
    DateTime? DueAtUtc,
    int? EstimateMinutes,
    int Version,
    string? IssueType = null,
    int? StoryPoints = null,
    Guid? EpicId = null,
    Guid? ParentTaskId = null,
    Guid? SprintId = null
);
