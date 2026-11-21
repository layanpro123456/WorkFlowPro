using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class ApprovalRequestStep : BaseEntity
{
    public Guid ApprovalRequestId { get; private set; }
    public int StepOrder { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid ApproverUserId { get; private set; }
    public string Status { get; private set; } = ApprovalStepStatuses.Pending;
    public DateTime? DecidedAtUtc { get; private set; }

    // Navigation properties for EF Core
    public ApprovalRequest? Request { get; private set; }
    public ApprovalDecision? Decision { get; private set; }

    // Private constructor for EF Core
    private ApprovalRequestStep() { }

    public ApprovalRequestStep(
        Guid approvalRequestId,
        int stepOrder,
        string name,
        Guid approverUserId)
    {
        if (approvalRequestId == Guid.Empty)
            throw new ArgumentException("ApprovalRequestId cannot be empty.", nameof(approvalRequestId));

        if (stepOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(stepOrder), "StepOrder must be positive (>= 1).");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Step name cannot be empty.", nameof(name));

        if (approverUserId == Guid.Empty)
            throw new ArgumentException("ApproverUserId cannot be empty.", nameof(approverUserId));

        ApprovalRequestId = approvalRequestId;
        StepOrder = stepOrder;
        Name = name.Trim();
        ApproverUserId = approverUserId;
        Status = ApprovalStepStatuses.Pending;
    }

    public ApprovalDecision RecordDecision(Guid approverId, string decision, string? comment = null)
    {
        if (Status != ApprovalStepStatuses.Pending)
        {
            throw new InvalidOperationException($"Cannot decide step {StepOrder} because it is already '{Status}'.");
        }

        if (ApproverUserId != approverId)
        {
            throw new ForbiddenException($"User {approverId} is not the designated approver for step {StepOrder} ('{Name}'). Only user {ApproverUserId} can decide this step.");
        }

        if (!ApprovalDecisions.IsValid(decision))
        {
            throw new ArgumentException($"Invalid decision '{decision}'.", nameof(decision));
        }

        var approvalDecision = new ApprovalDecision(Id, approverId, decision, comment);
        Decision = approvalDecision;
        Status = decision == ApprovalDecisions.Approved ? ApprovalStepStatuses.Approved : ApprovalStepStatuses.Rejected;
        DecidedAtUtc = approvalDecision.DecidedAtUtc;

        return approvalDecision;
    }

    public void MarkSkipped()
    {
        if (Status == ApprovalStepStatuses.Pending)
        {
            Status = ApprovalStepStatuses.Skipped;
        }
    }
}
