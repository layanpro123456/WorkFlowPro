namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record CreateTaskRequest(
    string Title,
    string? Description,
    string Priority,
    Guid? AssigneeId,
    DateTime? DueAtUtc,
    int? EstimateMinutes,
    string? Status = null,
    string IssueType = "Task",
    int? StoryPoints = null,
    Guid? EpicId = null,
    Guid? ParentTaskId = null,
    Guid? SprintId = null
);
