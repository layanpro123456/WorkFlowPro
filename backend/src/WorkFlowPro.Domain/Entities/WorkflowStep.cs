using WorkFlowPro.Domain.Common;

namespace WorkFlowPro.Domain.Entities;

public class WorkflowStep : BaseEntity
{
    public Guid WorkflowDefinitionId { get; private set; }
    public int StepOrder { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? ApproverRole { get; private set; }
    public Guid? ApproverUserId { get; private set; }

    // Navigation property for EF Core
    public WorkflowDefinition? WorkflowDefinition { get; private set; }

    // Private constructor for EF Core
    private WorkflowStep() { }

    public WorkflowStep(
        Guid workflowDefinitionId,
        int stepOrder,
        string name,
        string? approverRole,
        Guid? approverUserId)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw new ArgumentException("WorkflowDefinitionId cannot be empty.", nameof(workflowDefinitionId));

        if (stepOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(stepOrder), "StepOrder must be positive (>= 1).");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Step name cannot be empty.", nameof(name));

        ValidateApproverSelector(approverRole, approverUserId);

        WorkflowDefinitionId = workflowDefinitionId;
        StepOrder = stepOrder;
        Name = name.Trim();
        ApproverRole = string.IsNullOrWhiteSpace(approverRole) ? null : approverRole.Trim();
        ApproverUserId = approverUserId.HasValue && approverUserId.Value != Guid.Empty ? approverUserId : null;
    }

    public static void ValidateApproverSelector(string? approverRole, Guid? approverUserId)
    {
        var hasRole = !string.IsNullOrWhiteSpace(approverRole);
        var hasUser = approverUserId.HasValue && approverUserId.Value != Guid.Empty;

        if (!hasRole && !hasUser)
        {
            throw new ArgumentException("Step must specify exactly one approver selector: either an approver role or a concrete user ID.");
        }

        if (hasRole && hasUser)
        {
            throw new ArgumentException("Step cannot specify both an approver role and a concrete user ID. Choose exactly one selector.");
        }
    }
}
