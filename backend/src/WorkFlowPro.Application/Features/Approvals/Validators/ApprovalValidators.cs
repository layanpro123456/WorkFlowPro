using FluentValidation;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Approvals.Validators;

public class WorkflowStepInputDtoValidator : AbstractValidator<WorkflowStepInputDto>
{
    public WorkflowStepInputDtoValidator()
    {
        RuleFor(s => s.StepOrder)
            .GreaterThan(0).WithMessage("StepOrder must be greater than 0.");

        RuleFor(s => s.Name)
            .NotEmpty().WithMessage("Step name is required.")
            .MaximumLength(150).WithMessage("Step name cannot exceed 150 characters.");

        RuleFor(s => s)
            .Must(HaveExactlyOneSelector)
            .WithMessage("Each step must specify exactly one selector: either an approver role or a concrete user ID.");
    }

    private static bool HaveExactlyOneSelector(WorkflowStepInputDto s)
    {
        var hasRole = !string.IsNullOrWhiteSpace(s.ApproverRole);
        var hasUser = s.ApproverUserId.HasValue && s.ApproverUserId.Value != Guid.Empty;
        return (hasRole && !hasUser) || (!hasRole && hasUser);
    }
}

public class CreateWorkflowDefinitionRequestValidator : AbstractValidator<CreateWorkflowDefinitionRequest>
{
    public CreateWorkflowDefinitionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workflow name is required.")
            .MaximumLength(150).WithMessage("Workflow name cannot exceed 150 characters.");

        RuleFor(x => x.Steps)
            .NotEmpty().WithMessage("At least one approval step is required.")
            .Must(HaveSequentialStepOrders)
            .WithMessage("Workflow step orders must be sequential starting from 1 (1, 2, 3...) with no duplicates or gaps.");

        RuleForEach(x => x.Steps).SetValidator(new WorkflowStepInputDtoValidator());
    }

    private static bool HaveSequentialStepOrders(IReadOnlyList<WorkflowStepInputDto>? steps)
    {
        if (steps == null || steps.Count == 0) return false;
        var ordered = steps.OrderBy(s => s.StepOrder).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].StepOrder != i + 1) return false;
        }
        return true;
    }
}

public class UpdateWorkflowDefinitionRequestValidator : AbstractValidator<UpdateWorkflowDefinitionRequest>
{
    public UpdateWorkflowDefinitionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workflow name is required.")
            .MaximumLength(150).WithMessage("Workflow name cannot exceed 150 characters.");

        RuleFor(x => x.Steps)
            .NotEmpty().WithMessage("At least one approval step is required.")
            .Must(HaveSequentialStepOrders)
            .WithMessage("Workflow step orders must be sequential starting from 1 (1, 2, 3...) with no duplicates or gaps.");

        RuleForEach(x => x.Steps).SetValidator(new WorkflowStepInputDtoValidator());
    }

    private static bool HaveSequentialStepOrders(IReadOnlyList<WorkflowStepInputDto>? steps)
    {
        if (steps == null || steps.Count == 0) return false;
        var ordered = steps.OrderBy(s => s.StepOrder).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].StepOrder != i + 1) return false;
        }
        return true;
    }
}

public class SubmitApprovalRequestValidator : AbstractValidator<SubmitApprovalRequest>
{
    public SubmitApprovalRequestValidator()
    {
        RuleFor(x => x.WorkflowDefinitionId)
            .NotEmpty().WithMessage("WorkflowDefinitionId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Request title is required.")
            .MaximumLength(200).WithMessage("Request title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Request description is required.");
    }
}

public class RecordDecisionRequestValidator : AbstractValidator<RecordDecisionRequest>
{
    public RecordDecisionRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("Decision is required.")
            .Must(ApprovalDecisions.IsValid)
            .WithMessage("Decision must be either 'Approved' or 'Rejected'.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("ExpectedVersion must be a positive integer.");
    }
}

public class CancelApprovalRequestValidator : AbstractValidator<CancelApprovalRequest>
{
    public CancelApprovalRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("ExpectedVersion must be a positive integer.");
    }
}
