namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record AssignTicketRequest(
    Guid AssigneeId,
    string? Comment = null
);
