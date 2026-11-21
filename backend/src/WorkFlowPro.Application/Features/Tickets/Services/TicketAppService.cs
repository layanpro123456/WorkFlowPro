using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Tickets.Services;

public class TicketAppService : ITicketAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IValidator<CreateTicketRequest> _createValidator;
    private readonly IValidator<AssignTicketRequest> _assignValidator;
    private readonly IValidator<ChangeTicketStatusRequest> _statusValidator;
    private readonly IValidator<AddTicketMessageRequest> _messageValidator;

    private static readonly string[] AgentRoles =
    {
        Roles.SupportAgent,
        Roles.Manager,
        Roles.Administrator
    };

    public TicketAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IValidator<CreateTicketRequest> createValidator,
        IValidator<AssignTicketRequest> assignValidator,
        IValidator<ChangeTicketStatusRequest> statusValidator,
        IValidator<AddTicketMessageRequest> messageValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _createValidator = createValidator;
        _assignValidator = assignValidator;
        _statusValidator = statusValidator;
        _messageValidator = messageValidator;
    }

    public async Task<TicketSummaryDto> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();

        Project? project = null;
        if (request.RelatedProjectId.HasValue)
        {
            project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == request.RelatedProjectId.Value, cancellationToken);

            if (project == null)
            {
                throw new DomainException("Related project not found.");
            }

            var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);
            var isMember = project.Members.Any(m => m.UserId == callerId);
            if (!isAdmin && !isMember)
            {
                throw new DomainException("You do not have access to the specified related project.");
            }
        }

        var ticket = new SupportTicket(
            requesterId: callerId,
            subject: request.Subject,
            description: request.Description,
            category: request.Category,
            priority: request.Priority,
            relatedProjectId: request.RelatedProjectId
        );

        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        var callerUser = await _identityService.GetUserByIdAsync(callerId, cancellationToken);

        return new TicketSummaryDto(
            Id: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            Subject: ticket.Subject,
            Category: ticket.Category,
            Priority: ticket.Priority,
            Status: ticket.Status,
            RequesterId: ticket.RequesterId,
            RequesterName: callerUser?.DisplayName ?? "Unknown",
            AssigneeId: null,
            AssigneeName: null,
            RelatedProjectId: ticket.RelatedProjectId,
            RelatedProjectCode: project?.ProjectCode,
            RelatedProjectName: project?.Name,
            MessageCount: 0,
            CreatedAtUtc: ticket.CreatedAtUtc,
            UpdatedAtUtc: ticket.UpdatedAtUtc,
            ResolvedAtUtc: ticket.ResolvedAtUtc,
            ClosedAtUtc: ticket.ClosedAtUtc
        );
    }

    public async Task<PagedResult<TicketSummaryDto>> GetPagedListAsync(
        string? status,
        string? priority,
        string? category,
        Guid? assigneeId,
        string? search,
        bool myTicketsOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _context.SupportTickets
            .AsNoTracking()
            .Include(t => t.RelatedProject)
            .AsQueryable();

        // If not agent or if requester requested own tickets, restrict to requester
        if (!isAgent || myTicketsOnly)
        {
            query = query.Where(t => t.RequesterId == callerId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        if (assigneeId.HasValue)
        {
            query = query.Where(t => t.AssigneeId == assigneeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                t.Subject.ToLower().Contains(term) ||
                t.Description.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Category,
                t.Priority,
                t.Status,
                t.RequesterId,
                t.AssigneeId,
                t.RelatedProjectId,
                RelatedProjectCode = t.RelatedProject != null ? t.RelatedProject.ProjectCode : null,
                RelatedProjectName = t.RelatedProject != null ? t.RelatedProject.Name : null,
                MessageCount = t.Messages.Count(m => isAgent || !m.IsInternalNote),
                t.CreatedAtUtc,
                t.UpdatedAtUtc,
                t.ResolvedAtUtc,
                t.ClosedAtUtc
            })
            .ToListAsync(cancellationToken);

        var userIds = tickets.Select(t => t.RequesterId)
            .Concat(tickets.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value))
            .Distinct();

        var userMap = await _identityService.GetUsersSummaryAsync(userIds, cancellationToken);

        var items = tickets.Select(t => new TicketSummaryDto(
            Id: t.Id,
            TicketNumber: t.TicketNumber,
            Subject: t.Subject,
            Category: t.Category,
            Priority: t.Priority,
            Status: t.Status,
            RequesterId: t.RequesterId,
            RequesterName: userMap.TryGetValue(t.RequesterId, out var req) ? req.DisplayName : "Unknown",
            AssigneeId: t.AssigneeId,
            AssigneeName: t.AssigneeId.HasValue && userMap.TryGetValue(t.AssigneeId.Value, out var a) ? a.DisplayName : null,
            RelatedProjectId: t.RelatedProjectId,
            RelatedProjectCode: t.RelatedProjectCode,
            RelatedProjectName: t.RelatedProjectName,
            MessageCount: t.MessageCount,
            CreatedAtUtc: t.CreatedAtUtc,
            UpdatedAtUtc: t.UpdatedAtUtc,
            ResolvedAtUtc: t.ResolvedAtUtc,
            ClosedAtUtc: t.ClosedAtUtc
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<TicketSummaryDto>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<TicketDetailDto> GetByIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var ticket = await _context.SupportTickets
            .Include(t => t.RelatedProject)
            .Include(t => t.Messages)
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        if (!isAgent && ticket.RequesterId != callerId)
        {
            // Resource concealment: return 404 rather than revealing private ticket
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        // CRITICAL PRIVACY RULE: Filter internal notes for requesters
        var messages = ticket.Messages
            .Where(m => isAgent || !m.IsInternalNote)
            .OrderBy(m => m.CreatedAtUtc)
            .ToList();

        var userIds = new List<Guid> { ticket.RequesterId };
        if (ticket.AssigneeId.HasValue) userIds.Add(ticket.AssigneeId.Value);
        userIds.AddRange(messages.Select(m => m.AuthorId));
        userIds.AddRange(ticket.StatusHistory.Select(h => h.ChangedById));

        var userMap = await _identityService.GetUsersSummaryAsync(userIds.Distinct(), cancellationToken);

        var requesterUser = await _identityService.GetUserByIdAsync(ticket.RequesterId, cancellationToken);

        var messageDtos = messages.Select(m => new TicketMessageDto(
            Id: m.Id,
            TicketId: m.TicketId,
            AuthorId: m.AuthorId,
            AuthorName: userMap.TryGetValue(m.AuthorId, out var author) ? author.DisplayName : "Unknown",
            Body: m.Body,
            IsInternalNote: m.IsInternalNote,
            CreatedAtUtc: m.CreatedAtUtc
        )).ToList();

        var historyDtos = ticket.StatusHistory
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new TicketStatusHistoryDto(
                Id: h.Id,
                TicketId: h.TicketId,
                FromStatus: h.FromStatus,
                ToStatus: h.ToStatus,
                ChangedById: h.ChangedById,
                ChangedByName: userMap.TryGetValue(h.ChangedById, out var changer) ? changer.DisplayName : "Unknown",
                ChangedAtUtc: h.ChangedAtUtc,
                Comment: h.Comment
            )).ToList();

        return new TicketDetailDto(
            Id: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            Subject: ticket.Subject,
            Description: ticket.Description,
            Category: ticket.Category,
            Priority: ticket.Priority,
            Status: ticket.Status,
            RequesterId: ticket.RequesterId,
            RequesterName: userMap.TryGetValue(ticket.RequesterId, out var req) ? req.DisplayName : "Unknown",
            RequesterEmail: requesterUser?.Email ?? string.Empty,
            AssigneeId: ticket.AssigneeId,
            AssigneeName: ticket.AssigneeId.HasValue && userMap.TryGetValue(ticket.AssigneeId.Value, out var a) ? a.DisplayName : null,
            RelatedProjectId: ticket.RelatedProjectId,
            RelatedProjectCode: ticket.RelatedProject?.ProjectCode,
            RelatedProjectName: ticket.RelatedProject?.Name,
            CreatedAtUtc: ticket.CreatedAtUtc,
            UpdatedAtUtc: ticket.UpdatedAtUtc,
            ResolvedAtUtc: ticket.ResolvedAtUtc,
            ClosedAtUtc: ticket.ClosedAtUtc,
            Messages: messageDtos,
            History: historyDtos
        );
    }

    public async Task<TicketSummaryDto> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _assignValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();
        if (!IsAgentOrAdmin())
        {
            throw new UnauthorizedAccessException("Only support agents, managers, and administrators can assign tickets.");
        }

        var ticket = await _context.SupportTickets
            .Include(t => t.RelatedProject)
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        // Verify assignee is active and has an agent role
        var assigneeUser = await _identityService.GetUserByIdAsync(request.AssigneeId, cancellationToken);
        if (assigneeUser == null)
        {
            throw new DomainException("Target assignee does not exist.");
        }

        var isAssigneeActive = await _identityService.IsUserActiveAsync(request.AssigneeId, cancellationToken);
        if (!isAssigneeActive)
        {
            throw new DomainException("Cannot assign ticket to an inactive user.");
        }

        var assigneeRoles = await _identityService.GetUserRolesAsync(request.AssigneeId, cancellationToken);
        if (!assigneeRoles.Any(r => AgentRoles.Contains(r)))
        {
            throw new DomainException("Assignee must be an active support agent, manager, or administrator.");
        }

        var history = ticket.Assign(request.AssigneeId, callerId, request.Comment);
        if (history != null)
        {
            _context.TicketStatusHistories.Add(history);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var userIds = new List<Guid> { ticket.RequesterId };
        if (ticket.AssigneeId.HasValue) userIds.Add(ticket.AssigneeId.Value);
        var userMap = await _identityService.GetUsersSummaryAsync(userIds, cancellationToken);

        return MapToSummary(ticket, userMap);
    }

    public async Task<TicketSummaryDto> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var ticket = await _context.SupportTickets
            .Include(t => t.RelatedProject)
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        if (!isAgent)
        {
            // Requester can only close their own ticket
            if (ticket.RequesterId != callerId)
            {
                throw new KeyNotFoundException($"Ticket {ticketId} not found.");
            }

            if (request.Status != TicketStatuses.Closed)
            {
                throw new UnauthorizedAccessException("Requesters can only transition tickets to 'Closed'.");
            }
        }

        var history = ticket.ChangeStatus(request.Status, callerId, request.Comment);
        _context.TicketStatusHistories.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        var userIds = new List<Guid> { ticket.RequesterId };
        if (ticket.AssigneeId.HasValue) userIds.Add(ticket.AssigneeId.Value);
        var userMap = await _identityService.GetUsersSummaryAsync(userIds, cancellationToken);

        return MapToSummary(ticket, userMap);
    }

    public async Task<TicketMessageDto> AddMessageAsync(
        Guid ticketId,
        AddTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _messageValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        if (!isAgent && ticket.RequesterId != callerId)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        // CRITICAL PRIVACY RULE: Requesters cannot post internal notes
        if (request.IsInternalNote && !isAgent)
        {
            throw new UnauthorizedAccessException("Requesters cannot create internal notes.");
        }

        var message = ticket.AddMessage(callerId, request.Body, isAgent && request.IsInternalNote);
        _context.TicketMessages.Add(message);

        // If ticket was WaitingForRequester and requester responds, auto-move to InProgress
        if (!isAgent && ticket.Status == TicketStatuses.WaitingForRequester)
        {
            var history = ticket.ChangeStatus(TicketStatuses.InProgress, callerId, "Requester replied.");
            _context.TicketStatusHistories.Add(history);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var callerUser = await _identityService.GetUserByIdAsync(callerId, cancellationToken);

        return new TicketMessageDto(
            Id: message.Id,
            TicketId: message.TicketId,
            AuthorId: message.AuthorId,
            AuthorName: callerUser?.DisplayName ?? "Unknown",
            Body: message.Body,
            IsInternalNote: message.IsInternalNote,
            CreatedAtUtc: message.CreatedAtUtc
        );
    }

    public async Task<IReadOnlyList<TicketMessageDto>> GetMessagesAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var ticket = await _context.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        if (!isAgent && ticket.RequesterId != callerId)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        var messages = ticket.Messages
            .Where(m => isAgent || !m.IsInternalNote)
            .OrderBy(m => m.CreatedAtUtc)
            .ToList();

        var authorIds = messages.Select(m => m.AuthorId).Distinct();
        var userMap = await _identityService.GetUsersSummaryAsync(authorIds, cancellationToken);

        return messages.Select(m => new TicketMessageDto(
            Id: m.Id,
            TicketId: m.TicketId,
            AuthorId: m.AuthorId,
            AuthorName: userMap.TryGetValue(m.AuthorId, out var author) ? author.DisplayName : "Unknown",
            Body: m.Body,
            IsInternalNote: m.IsInternalNote,
            CreatedAtUtc: m.CreatedAtUtc
        )).ToList();
    }

    public async Task<IReadOnlyList<TicketStatusHistoryDto>> GetHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var ticket = await _context.SupportTickets
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        if (!isAgent && ticket.RequesterId != callerId)
        {
            throw new KeyNotFoundException($"Ticket {ticketId} not found.");
        }

        var changerIds = ticket.StatusHistory.Select(h => h.ChangedById).Distinct();
        var userMap = await _identityService.GetUsersSummaryAsync(changerIds, cancellationToken);

        return ticket.StatusHistory
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new TicketStatusHistoryDto(
                Id: h.Id,
                TicketId: h.TicketId,
                FromStatus: h.FromStatus,
                ToStatus: h.ToStatus,
                ChangedById: h.ChangedById,
                ChangedByName: userMap.TryGetValue(h.ChangedById, out var changer) ? changer.DisplayName : "Unknown",
                ChangedAtUtc: h.ChangedAtUtc,
                Comment: h.Comment
            )).ToList();
    }

    public async Task<IReadOnlyList<SupportAgentDto>> GetEligibleAssigneesAsync(
        CancellationToken cancellationToken)
    {
        if (!IsAgentOrAdmin())
        {
            throw new UnauthorizedAccessException("Only support agents, managers, and administrators can view assignees.");
        }

        var agents = await _identityService.GetUsersInRolesAsync(AgentRoles, cancellationToken);

        return agents.Select(a => new SupportAgentDto(
            UserId: a.UserId,
            DisplayName: a.DisplayName,
            Email: a.Email,
            PrimaryRole: a.Roles.FirstOrDefault(r => AgentRoles.Contains(r)) ?? "SupportAgent"
        )).ToList();
    }

    private bool IsAgentOrAdmin()
    {
        return _currentUserService.Roles.Any(r => AgentRoles.Contains(r));
    }

    private Guid RequireCallerId()
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }
        return _currentUserService.UserId.Value;
    }

    private static TicketSummaryDto MapToSummary(
        SupportTicket ticket,
        IDictionary<Guid, (string DisplayName, string Email)> userMap)
    {
        return new TicketSummaryDto(
            Id: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            Subject: ticket.Subject,
            Category: ticket.Category,
            Priority: ticket.Priority,
            Status: ticket.Status,
            RequesterId: ticket.RequesterId,
            RequesterName: userMap.TryGetValue(ticket.RequesterId, out var req) ? req.DisplayName : "Unknown",
            AssigneeId: ticket.AssigneeId,
            AssigneeName: ticket.AssigneeId.HasValue && userMap.TryGetValue(ticket.AssigneeId.Value, out var a) ? a.DisplayName : null,
            RelatedProjectId: ticket.RelatedProjectId,
            RelatedProjectCode: ticket.RelatedProject?.ProjectCode,
            RelatedProjectName: ticket.RelatedProject?.Name,
            MessageCount: ticket.Messages.Count,
            CreatedAtUtc: ticket.CreatedAtUtc,
            UpdatedAtUtc: ticket.UpdatedAtUtc,
            ResolvedAtUtc: ticket.ResolvedAtUtc,
            ClosedAtUtc: ticket.ClosedAtUtc
        );
    }
}
