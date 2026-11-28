using WorkFlowPro.Application.Features.Git.Dtos;

namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record TaskDetailDto(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid CreatedById,
    string CreatedByName,
    DateTime? DueAtUtc,
    int? EstimateMinutes,
    int Version,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<TaskCommentDto> Comments,
    IReadOnlyList<TaskStatusHistoryDto> History,
    int TaskNumber = 0,
    string TaskKey = "",
    IReadOnlyList<TaskGitBranchDto>? Branches = null,
    IReadOnlyList<TaskPullRequestDto>? PullRequests = null
);
