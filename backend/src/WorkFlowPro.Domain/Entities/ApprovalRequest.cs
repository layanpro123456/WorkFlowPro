using WorkFlowPro.Domain.Common;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Domain.Entities;

public class ApprovalRequest : BaseEntity
{
    private readonly List<ApprovalRequestStep> _steps = new();

    public long RequestNumber { get; private set; }
    public Guid WorkflowDefinitionId { get; private set; }
    public Guid RequesterId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Status { get; private set; } = ApprovalRequestStatuses.Pending;
    public int? CurrentStepOrder { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int Version { get; private set; } = 1;

    // Navigation properties for EF Core
    public WorkflowDefinition? WorkflowDefinition { get; private set; }
    public IReadOnlyCollection<ApprovalRequestStep> Steps => _steps.AsReadOnly();

    // Private constructor for EF Core
    private ApprovalRequest() { }

    public ApprovalRequest(
        Guid workflowDefinitionId,
        Guid requesterId,
        string title,
        string description)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw new ArgumentException("WorkflowDefinitionId cannot be empty.", nameof(workflowDefinitionId));

        if (requesterId == Guid.Empty)
            throw new ArgumentException("RequesterId cannot be empty.", nameof(requesterId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        WorkflowDefinitionId = workflowDefinitionId;
        RequesterId = requesterId;
        Title = title.Trim();
        Description = description.Trim();
        Status = ApprovalRequestStatuses.Pending;
        SubmittedAtUtc = DateTime.UtcNow;
        Version = 1;
    }

    public void AssignRequestNumber(long requestNumber)
    {
        if (RequestNumber == 0 && requestNumber > 0)
        {
            RequestNumber = requestNumber;
        }
    }

    public ApprovalRequestStep AddStepSnapshot(int stepOrder, string name, Guid approverUserId)
    {
        if (Status != ApprovalRequestStatuses.Pending)
        {
            throw new InvalidOperationException("Cannot add steps to a finalized approval request.");
        }

        var step = new ApprovalRequestStep(Id, stepOrder, name, approverUserId);
        _steps.Add(step);

        if (!CurrentStepOrder.HasValue || CurrentStepOrder.Value > stepOrder)
        {
            CurrentStepOrder = 1;
        }

        return step;
    }

    public ApprovalDecision ProcessDecision(Guid approverId, string decision, string? comment, int expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new ConcurrencyException(
                $"Concurrency conflict: expected version {expectedVersion}, but request is currently at version {Version}. Please refresh the latest state before deciding.");
        }

        if (Status != ApprovalRequestStatuses.Pending)
        {
            throw new InvalidOperationException($"Cannot record decision on request in '{Status}' status.");
        }

        if (!CurrentStepOrder.HasValue)
        {
            throw new InvalidOperationException("Request has no active current step.");
        }

        var currentStep = _steps.FirstOrDefault(s => s.StepOrder == CurrentStepOrder.Value)
            ?? throw new InvalidOperationException($"Current step {CurrentStepOrder.Value} could not be found.");

        var approvalDecision = currentStep.RecordDecision(approverId, decision, comment);

        if (decision == ApprovalDecisions.Approved)
        {
            var nextStep = _steps
                .Where(s => s.StepOrder > currentStep.StepOrder && s.Status == ApprovalStepStatuses.Pending)
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault();

            if (nextStep != null)
            {
                CurrentStepOrder = nextStep.StepOrder;
            }
            else
            {
                // All steps completed and approved!
                Status = ApprovalRequestStatuses.Approved;
                CompletedAtUtc = DateTime.UtcNow;
                CurrentStepOrder = null;
            }
        }
        else if (decision == ApprovalDecisions.Rejected)
        {
            // Rejection terminates the request immediately and skips remaining steps
            foreach (var remainingStep in _steps.Where(s => s.StepOrder > currentStep.StepOrder))
            {
                remainingStep.MarkSkipped();
            }

            Status = ApprovalRequestStatuses.Rejected;
            CompletedAtUtc = DateTime.UtcNow;
            CurrentStepOrder = null;
        }

        Version++;
        return approvalDecision;
    }

    public void Cancel(Guid cancelledById, int expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new ConcurrencyException(
                $"Concurrency conflict: expected version {expectedVersion}, but request is currently at version {Version}.");
        }

        if (Status != ApprovalRequestStatuses.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel request in '{Status}' status.");
        }

        foreach (var step in _steps.Where(s => s.Status == ApprovalStepStatuses.Pending))
        {
            step.MarkSkipped();
        }

        Status = ApprovalRequestStatuses.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
        CurrentStepOrder = null;
        Version++;
    }
}
