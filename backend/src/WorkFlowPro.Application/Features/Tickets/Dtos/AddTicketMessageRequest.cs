namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record AddTicketMessageRequest(
    string Body,
    bool IsInternalNote = false
);
