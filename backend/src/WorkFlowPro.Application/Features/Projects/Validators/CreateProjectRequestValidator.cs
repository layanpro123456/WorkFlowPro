using FluentValidation;
using WorkFlowPro.Application.Features.Projects.Dtos;

namespace WorkFlowPro.Application.Features.Projects.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.ProjectCode)
            .NotEmpty().WithMessage("Project code is required.")
            .MaximumLength(30).WithMessage("Project code cannot exceed 30 characters.")
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Project code may only contain alphanumeric characters, underscores, and hyphens.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(150).WithMessage("Project name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.");

        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.TargetDate.HasValue || x.TargetDate.Value >= x.StartDate.Value)
            .WithMessage("Target date cannot be earlier than start date.");
    }
}
