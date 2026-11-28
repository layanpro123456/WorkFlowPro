namespace WorkFlowPro.Application.Features.Sprints.Dtos;

public record SprintDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    string Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TotalTasks,
    int CompletedTasks,
    int TotalPoints,
    int CompletedPoints,
    DateTime CreatedAtUtc
);

public record CreateSprintRequest(
    string Name,
    string? Goal = null,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null
);

public record UpdateSprintRequest(
    string Name,
    string? Goal = null,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null
);

public record StartSprintRequest(
    DateTime StartDateUtc,
    DateTime EndDateUtc
);

public record CompleteSprintRequest(
    Guid? MoveIncompleteToSprintId = null
);

public record CompleteSprintResultDto(
    SprintDto CompletedSprint,
    int CompletedTasksCount,
    int RolledOverTasksCount,
    Guid? RolledOverToSprintId
);

public record SprintBurndownPointDto(
    DateOnly Date,
    int IdealRemainingPoints,
    int ActualRemainingPoints,
    int CompletedPoints,
    int RemainingTaskCount
);

public record SprintBurndownDto(
    Guid SprintId,
    string SprintName,
    string Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    int TotalPoints,
    IReadOnlyList<SprintBurndownPointDto> Points
);

public record VelocitySprintBarDto(
    Guid SprintId,
    string SprintName,
    int CommittedPoints,
    int CompletedPoints,
    int CompletedTasks,
    DateTime? CompletedAtUtc
);

public record VelocityChartDto(
    Guid ProjectId,
    double AverageCompletedPoints,
    IReadOnlyList<VelocitySprintBarDto> Sprints
);

public record BoardColumnConfigDto(
    string Status,
    int DisplayOrder,
    int? WipLimit,
    bool IsStrictWip,
    bool IsVisible
);

public record UpdateBoardColumnConfigsRequest(
    IReadOnlyList<BoardColumnConfigDto> Columns
);

public record MoveTaskOnBoardRequest(
    string TargetStatus,
    int BoardRank,
    int Version,
    string? Comment = null
);

public record AssignTaskSprintRequest(
    Guid? SprintId,
    int BoardRank = 0
);

public record IssueLinkDto(
    Guid Id,
    string LinkType,
    string DisplayRelation,
    Guid RelatedTaskId,
    string RelatedTaskKey,
    string RelatedTaskTitle,
    string RelatedTaskStatus,
    string RelatedTaskIssueType,
    DateTime CreatedAtUtc
);

public record CreateIssueLinkRequest(
    Guid TargetTaskId,
    string LinkType
);
