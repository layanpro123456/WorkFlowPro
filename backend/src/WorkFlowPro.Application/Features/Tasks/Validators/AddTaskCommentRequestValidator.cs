using FluentValidation;
using WorkFlowPro.Application.Features.Tasks.Dtos;

namespace WorkFlowPro.Application.Features.Tasks.Validators;

public class AddTaskCommentRequestValidator : AbstractValidator<AddTaskCommentRequest>
{
    public AddTaskCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Comment body is required.")
            .MaximumLength(4000).WithMessage("Comment body cannot exceed 4000 characters.");
    }
}
