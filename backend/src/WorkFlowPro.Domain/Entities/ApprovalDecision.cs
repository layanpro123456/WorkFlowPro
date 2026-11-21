using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Domain.Entities;

public class ApprovalDecision : BaseEntity
{
    public Guid ApprovalRequestStepId { get; private set; }
    public Guid ApproverId { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public string? Comment { get; private set; }
    public DateTime DecidedAtUtc { get; private set; }

    // Navigation property for EF Core
    public ApprovalRequestStep? Step { get; private set; }

    // Private constructor for EF Core
    private ApprovalDecision() { }

    public ApprovalDecision(
        Guid approvalRequestStepId,
        Guid approverId,
        string decision,
        string? comment = null)
    {
        if (approvalRequestStepId == Guid.Empty)
            throw new ArgumentException("ApprovalRequestStepId cannot be empty.", nameof(approvalRequestStepId));

        if (approverId == Guid.Empty)
            throw new ArgumentException("ApproverId cannot be empty.", nameof(approverId));

        if (!ApprovalDecisions.IsValid(decision))
            throw new ArgumentException($"Invalid decision '{decision}'. Must be 'Approved' or 'Rejected'.", nameof(decision));

        ApprovalRequestStepId = approvalRequestStepId;
        ApproverId = approverId;
        Decision = decision;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        DecidedAtUtc = DateTime.UtcNow;
    }
}
