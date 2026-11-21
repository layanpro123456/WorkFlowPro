namespace WorkFlowPro.Application.Features.Approvals.Dtos;

public record WorkflowDefinitionSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int StepCount,
    Guid CreatedById,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record WorkflowStepDto(
    Guid Id,
    int StepOrder,
    string Name,
    string? ApproverRole,
    Guid? ApproverUserId,
    string? ApproverDisplayName
);

public record WorkflowDefinitionDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    Guid CreatedById,
    string CreatedByName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<WorkflowStepDto> Steps
);

public record WorkflowStepInputDto(
    int StepOrder,
    string Name,
    string? ApproverRole,
    Guid? ApproverUserId
);

public record CreateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    IReadOnlyList<WorkflowStepInputDto> Steps
);

public record UpdateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<WorkflowStepInputDto> Steps
);

public record StepApproverOverrideDto(
    int StepOrder,
    Guid ApproverUserId
);

public record SubmitApprovalRequest(
    Guid WorkflowDefinitionId,
    string Title,
    string Description,
    IReadOnlyList<StepApproverOverrideDto>? StepApproverOverrides
);

public record ApprovalDecisionDto(
    Guid Id,
    Guid ApproverId,
    string ApproverName,
    string Decision,
    string? Comment,
    DateTime DecidedAtUtc
);

public record ApprovalRequestStepDto(
    Guid Id,
    int StepOrder,
    string Name,
    Guid ApproverUserId,
    string ApproverName,
    string ApproverEmail,
    string Status,
    DateTime? DecidedAtUtc,
    ApprovalDecisionDto? Decision
);

public record ApprovalRequestSummaryDto(
    Guid Id,
    long RequestNumber,
    Guid WorkflowDefinitionId,
    string WorkflowName,
    Guid RequesterId,
    string RequesterName,
    string Title,
    string Status,
    int? CurrentStepOrder,
    string? CurrentStepName,
    Guid? CurrentApproverId,
    string? CurrentApproverName,
    int Version,
    DateTime SubmittedAtUtc,
    DateTime? CompletedAtUtc
);

public record ApprovalRequestDetailDto(
    Guid Id,
    long RequestNumber,
    Guid WorkflowDefinitionId,
    string WorkflowName,
    Guid RequesterId,
    string RequesterName,
    string RequesterEmail,
    string Title,
    string Description,
    string Status,
    int? CurrentStepOrder,
    string? CurrentStepName,
    Guid? CurrentApproverId,
    string? CurrentApproverName,
    int Version,
    DateTime SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<ApprovalRequestStepDto> Steps
);

public record RecordDecisionRequest(
    string Decision,
    string? Comment,
    int ExpectedVersion
);

public record CancelApprovalRequest(
    int ExpectedVersion
);
