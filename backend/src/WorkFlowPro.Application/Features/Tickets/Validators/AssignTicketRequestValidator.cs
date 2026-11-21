using FluentValidation;
using WorkFlowPro.Application.Features.Tickets.Dtos;

namespace WorkFlowPro.Application.Features.Tickets.Validators;

public class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(x => x.AssigneeId)
            .NotEmpty().WithMessage("Assignee ID is required.");
    }
}
