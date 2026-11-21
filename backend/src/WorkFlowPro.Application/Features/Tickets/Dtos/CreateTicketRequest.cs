namespace WorkFlowPro.Application.Features.Tickets.Dtos;

public record CreateTicketRequest(
    string Subject,
    string Description,
    string Category,
    string Priority,
    Guid? RelatedProjectId = null
);
