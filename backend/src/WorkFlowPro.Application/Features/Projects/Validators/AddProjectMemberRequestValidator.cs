using FluentValidation;
using WorkFlowPro.Application.Features.Projects.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Projects.Validators;

public class AddProjectMemberRequestValidator : AbstractValidator<AddProjectMemberRequest>
{
    public AddProjectMemberRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.ProjectRole)
            .NotEmpty().WithMessage("ProjectRole is required.")
            .Must(r => ProjectRoles.All.Contains(r))
            .WithMessage($"ProjectRole must be one of: {string.Join(", ", ProjectRoles.All)}");
    }
}
