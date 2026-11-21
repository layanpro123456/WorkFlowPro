using FluentValidation;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Projects.Validators;

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(150).WithMessage("Project name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || ProjectStatuses.All.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ProjectStatuses.All)}");

        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.TargetDate.HasValue || x.TargetDate.Value >= x.StartDate.Value)
            .WithMessage("Target date cannot be earlier than start date.");
    }
}
