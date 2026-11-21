using FluentValidation;
using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Tickets.Validators;

public class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Target status is required.")
            .Must(s => TicketStatuses.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", TicketStatuses.All)}");
    }
}
