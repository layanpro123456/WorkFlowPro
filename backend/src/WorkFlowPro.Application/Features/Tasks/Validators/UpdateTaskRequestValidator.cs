using FluentValidation;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Tasks.Validators;

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(200).WithMessage("Task title cannot exceed 200 characters.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(p => TaskPriorities.All.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Priority must be one of: {string.Join(", ", TaskPriorities.All)}");

        RuleFor(x => x.EstimateMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EstimateMinutes.HasValue)
            .WithMessage("Estimate minutes cannot be negative.");

        RuleFor(x => x.Version)
            .GreaterThan(0)
            .WithMessage("Version must be greater than zero.");
    }
}
