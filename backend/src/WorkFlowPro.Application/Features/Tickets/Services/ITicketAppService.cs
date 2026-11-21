using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Tickets.Dtos;

namespace WorkFlowPro.Application.Features.Tickets.Services;

public interface ITicketAppService
{
    Task<TicketSummaryDto> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<TicketSummaryDto>> GetPagedListAsync(
        string? status,
        string? priority,
        string? category,
        Guid? assigneeId,
        string? search,
        bool myTicketsOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<TicketDetailDto> GetByIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken);

    Task<TicketSummaryDto> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        CancellationToken cancellationToken);

    Task<TicketSummaryDto> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken);

    Task<TicketMessageDto> AddMessageAsync(
        Guid ticketId,
        AddTicketMessageRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketMessageDto>> GetMessagesAsync(
        Guid ticketId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketStatusHistoryDto>> GetHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupportAgentDto>> GetEligibleAssigneesAsync(
        CancellationToken cancellationToken);
}
