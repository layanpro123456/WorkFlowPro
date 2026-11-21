using FluentValidation;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Tasks.Validators;

public class ChangeTaskStatusRequestValidator : AbstractValidator<ChangeTaskStatusRequest>
{
    public ChangeTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Target status is required.")
            .Must(s => TaskStatuses.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", TaskStatuses.All)}");

        RuleFor(x => x.Version)
            .GreaterThan(0)
            .WithMessage("Version must be greater than zero.");
    }
}
