using FluentValidation;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Tasks.Validators;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
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

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || TaskStatuses.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", TaskStatuses.All)}");

        RuleFor(x => x.IssueType)
            .Must(IssueTypes.IsValid)
            .WithMessage($"IssueType must be one of: {string.Join(", ", IssueTypes.All)}");

        RuleFor(x => x.StoryPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => x.StoryPoints.HasValue)
            .WithMessage("Story points cannot be negative.");
    }
}
