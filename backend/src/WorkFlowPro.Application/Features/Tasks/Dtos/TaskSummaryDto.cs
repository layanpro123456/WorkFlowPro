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
    DateTime? UpdatedAtUtc
);
