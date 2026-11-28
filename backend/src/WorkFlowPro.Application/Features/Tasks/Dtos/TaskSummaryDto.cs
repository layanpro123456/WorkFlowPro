using WorkFlowPro.Application.Features.Git.Dtos;

namespace WorkFlowPro.Application.Features.Tasks.Dtos;

public record TaskSummaryDto(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string Title,
    string Status,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid CreatedById,
    string CreatedByName,
    DateTime? DueAtUtc,
    int? EstimateMinutes,
    int Version,
    int CommentCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int TaskNumber = 0,
    string TaskKey = "",
    TaskGitSummaryDto? GitSummary = null
);
