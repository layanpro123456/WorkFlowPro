namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record ChangeTicketStatusRequest(
    string Status,
    string? Comment = null
);
