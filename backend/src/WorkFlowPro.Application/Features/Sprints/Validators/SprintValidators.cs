using FluentValidation;
using WorkFlowPro.Application.Features.Sprints.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Sprints.Validators;

public class CreateSprintRequestValidator : AbstractValidator<CreateSprintRequest>
{
    public CreateSprintRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sprint name is required.")
            .MaximumLength(120).WithMessage("Sprint name cannot exceed 120 characters.");

        RuleFor(x => x.Goal)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Goal))
            .WithMessage("Sprint goal cannot exceed 500 characters.");

        RuleFor(x => x.EndDateUtc)
            .GreaterThan(x => x.StartDateUtc!.Value)
            .When(x => x.StartDateUtc.HasValue && x.EndDateUtc.HasValue)
            .WithMessage("Sprint end date must be after start date.");
    }
}

public class UpdateSprintRequestValidator : AbstractValidator<UpdateSprintRequest>
{
    public UpdateSprintRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sprint name is required.")
            .MaximumLength(120);

        RuleFor(x => x.Goal)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Goal));

        RuleFor(x => x.EndDateUtc)
            .GreaterThan(x => x.StartDateUtc!.Value)
            .When(x => x.StartDateUtc.HasValue && x.EndDateUtc.HasValue)
            .WithMessage("Sprint end date must be after start date.");
    }
}

public class StartSprintRequestValidator : AbstractValidator<StartSprintRequest>
{
    public StartSprintRequestValidator()
    {
        RuleFor(x => x.EndDateUtc)
            .GreaterThan(x => x.StartDateUtc)
            .WithMessage("Sprint end date must be after start date.");
    }
}

public class MoveTaskOnBoardRequestValidator : AbstractValidator<MoveTaskOnBoardRequest>
{
    public MoveTaskOnBoardRequestValidator()
    {
        RuleFor(x => x.TargetStatus)
            .NotEmpty()
            .Must(TaskStatuses.IsValid)
            .WithMessage($"TargetStatus must be one of: {string.Join(", ", TaskStatuses.All)}");

        RuleFor(x => x.Version)
            .GreaterThan(0);

        RuleFor(x => x.BoardRank)
            .GreaterThanOrEqualTo(0);
    }
}

public class UpdateBoardColumnConfigsRequestValidator : AbstractValidator<UpdateBoardColumnConfigsRequest>
{
    public UpdateBoardColumnConfigsRequestValidator()
    {
        RuleFor(x => x.Columns)
            .NotEmpty().WithMessage("Columns configuration is required.");

        RuleForEach(x => x.Columns).ChildRules(col =>
        {
            col.RuleFor(c => c.Status).Must(TaskStatuses.IsValid).WithMessage("Invalid column status.");
            col.RuleFor(c => c.WipLimit)
                .GreaterThanOrEqualTo(1)
                .When(c => c.WipLimit.HasValue)
                .WithMessage("WIP limit must be at least 1.");
        });
    }
}

public class CreateIssueLinkRequestValidator : AbstractValidator<CreateIssueLinkRequest>
{
    public CreateIssueLinkRequestValidator()
    {
        RuleFor(x => x.TargetTaskId).NotEmpty();
        RuleFor(x => x.LinkType)
            .NotEmpty()
            .Must(IssueLinkTypes.IsValid)
            .WithMessage($"LinkType must be one of: {string.Join(", ", IssueLinkTypes.All)}");
    }
}
