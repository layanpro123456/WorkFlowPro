using FluentValidation;
using WorkFlowPro.Application.Features.Tickets.Dtos;

namespace WorkFlowPro.Application.Features.Tickets.Validators;

public class AddTicketMessageRequestValidator : AbstractValidator<AddTicketMessageRequest>
{
    public AddTicketMessageRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Message body is required.")
            .MaximumLength(4000).WithMessage("Message body cannot exceed 4000 characters.");
    }
}
