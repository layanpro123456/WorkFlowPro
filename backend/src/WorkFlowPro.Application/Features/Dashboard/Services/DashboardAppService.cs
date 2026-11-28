using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Application.Features.Dashboard.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Application.Features.Tickets.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Dashboard.Services;

public class DashboardAppService : IDashboardAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    private static readonly string[] AgentRoles = { Roles.SupportAgent, Roles.Manager, Roles.Administrator };
    private static readonly string[] ManagerOrAdminRoles = { Roles.Manager, Roles.Administrator };

    public DashboardAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    private Guid RequireCallerId() =>
        _currentUserService.UserId ?? throw new UnauthorizedAccessException("Authentication required.");

    private bool IsAgentOrAdmin() =>
        _currentUserService.Roles.Any(r => AgentRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    private bool IsManagerOrAdmin() =>
        _currentUserService.Roles.Any(r => ManagerOrAdminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var now = DateTime.UtcNow;
        var isAgent = IsAgentOrAdmin();
        var isManager = IsManagerOrAdmin();

        // 1. Tasks Counts
        var activeAssignedTasksQuery = _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssigneeId == callerId &&
                        t.Status != TaskStatuses.Done &&
                        t.Status != TaskStatuses.Cancelled);

        var assignedTasksCount = await activeAssignedTasksQuery.CountAsync(cancellationToken);

        var overdueTasksCount = await activeAssignedTasksQuery
            .Where(t => t.DueAtUtc.HasValue && t.DueAtUtc.Value < now)
            .CountAsync(cancellationToken);

        // 2. Tickets Count
        var ticketsQuery = _context.SupportTickets.AsNoTracking();
        int openTicketsCount;
        if (isAgent)
        {
            openTicketsCount = await ticketsQuery
                .Where(t => t.Status != TicketStatuses.Resolved && t.Status != TicketStatuses.Closed)
                .CountAsync(cancellationToken);
        }
        else
        {
            openTicketsCount = await ticketsQuery
                .Where(t => t.RequesterId == callerId &&
                            t.Status != TicketStatuses.Resolved &&
                            t.Status != TicketStatuses.Closed)
                .CountAsync(cancellationToken);
        }

        // 3. Pending Approvals Awaiting Caller
        var pendingApprovalsCount = await _context.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.Status == ApprovalRequestStatuses.Pending &&
                        r.CurrentStepOrder.HasValue &&
                        r.Steps.Any(s => s.StepOrder == r.CurrentStepOrder && s.ApproverUserId == callerId))
            .CountAsync(cancellationToken);

        // 4. Unread Notifications Count
        var unreadNotificationsCount = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == callerId && n.ReadAtUtc == null)
            .CountAsync(cancellationToken);

        // 5. Managed Projects Count (for Managers/Admins)
        int? managedProjectsCount = null;
        if (isManager)
        {
            if (_currentUserService.Roles.Contains(Roles.Administrator, StringComparer.OrdinalIgnoreCase))
            {
                managedProjectsCount = await _context.Projects
                    .AsNoTracking()
                    .Where(p => p.Status == ProjectStatuses.Active)
                    .CountAsync(cancellationToken);
            }
            else
            {
                managedProjectsCount = await _context.Projects
                    .AsNoTracking()
                    .Where(p => p.Status == ProjectStatuses.Active &&
                                (p.CreatedById == callerId ||
                                 p.Members.Any(m => m.UserId == callerId &&
                                                   (m.ProjectRole == ProjectRoles.Owner || m.ProjectRole == ProjectRoles.Manager))))
                    .CountAsync(cancellationToken);
            }
        }

        // 6. Recent Tasks Visible to Caller
        var userProjectIds = await _context.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId)
            .Select(m => m.ProjectId)
            .ToListAsync(cancellationToken);

        var recentTasksQuery = _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Comments)
            .AsQueryable();

        if (!_currentUserService.Roles.Contains(Roles.Administrator, StringComparer.OrdinalIgnoreCase))
        {
            recentTasksQuery = recentTasksQuery.Where(t => userProjectIds.Contains(t.ProjectId));
        }

        var recentTaskEntities = await recentTasksQuery
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var taskUserIds = recentTaskEntities
            .Select(t => t.CreatedById)
            .Concat(recentTaskEntities.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value))
            .Distinct()
            .ToList();

        var taskUserSummaries = await _identityService.GetUsersSummaryAsync(taskUserIds, cancellationToken);

        var recentTasks = recentTaskEntities.Select(t =>
        {
            var createdByName = taskUserSummaries.TryGetValue(t.CreatedById, out var c) ? c.DisplayName : "Unknown";
            string? assigneeName = null;
            if (t.AssigneeId.HasValue && taskUserSummaries.TryGetValue(t.AssigneeId.Value, out var a))
            {
                assigneeName = a.DisplayName;
            }

            return new TaskSummaryDto(
                t.Id,
                t.ProjectId,
                t.Project?.ProjectCode ?? string.Empty,
                t.Project?.Name ?? string.Empty,
                t.Title,
                t.Status,
                t.Priority,
                t.AssigneeId,
                assigneeName,
                t.CreatedById,
                createdByName,
                t.DueAtUtc,
                t.EstimateMinutes,
                t.Version,
                t.Comments.Count,
                t.CreatedAtUtc,
                t.UpdatedAtUtc,
                t.TaskNumber,
                t.TaskKey
            );
        }).ToList();

        // 7. Recent Tickets
        var recentTicketEntitiesQuery = _context.SupportTickets
            .AsNoTracking()
            .Include(t => t.RelatedProject)
            .Include(t => t.Messages)
            .AsQueryable();

        if (!isAgent)
        {
            recentTicketEntitiesQuery = recentTicketEntitiesQuery.Where(t => t.RequesterId == callerId);
        }

        var recentTicketEntities = await recentTicketEntitiesQuery
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var ticketUserIds = recentTicketEntities
            .Select(t => t.RequesterId)
            .Concat(recentTicketEntities.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value))
            .Distinct()
            .ToList();

        var ticketUserSummaries = await _identityService.GetUsersSummaryAsync(ticketUserIds, cancellationToken);

        var recentTickets = recentTicketEntities.Select(t =>
        {
            var requesterName = ticketUserSummaries.TryGetValue(t.RequesterId, out var req) ? req.DisplayName : "Requester";
            string? assigneeName = null;
            if (t.AssigneeId.HasValue && ticketUserSummaries.TryGetValue(t.AssigneeId.Value, out var ass))
            {
                assigneeName = ass.DisplayName;
            }

            var messageCount = isAgent
                ? t.Messages.Count
                : t.Messages.Count(m => !m.IsInternalNote);

            return new TicketSummaryDto(
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Category,
                t.Priority,
                t.Status,
                t.RequesterId,
                requesterName,
                t.AssigneeId,
                assigneeName,
                t.RelatedProjectId,
                t.RelatedProject?.ProjectCode,
                t.RelatedProject?.Name,
                messageCount,
                t.CreatedAtUtc,
                t.UpdatedAtUtc,
                t.ResolvedAtUtc,
                t.ClosedAtUtc
            );
        }).ToList();

        // 8. Recent Approvals Awaiting Caller
        var recentApprovalEntities = await _context.ApprovalRequests
            .AsNoTracking()
            .Include(r => r.WorkflowDefinition)
            .Include(r => r.Steps)
            .Where(r => r.Status == ApprovalRequestStatuses.Pending &&
                        r.CurrentStepOrder.HasValue &&
                        r.Steps.Any(s => s.StepOrder == r.CurrentStepOrder && s.ApproverUserId == callerId))
            .OrderByDescending(r => r.SubmittedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var approvalUserIds = recentApprovalEntities
            .Select(r => r.RequesterId)
            .Concat(new[] { callerId })
            .Distinct()
            .ToList();

        var approvalUserSummaries = await _identityService.GetUsersSummaryAsync(approvalUserIds, cancellationToken);
        var callerDisplayName = approvalUserSummaries.TryGetValue(callerId, out var me) ? me.DisplayName : "You";

        var recentApprovals = recentApprovalEntities.Select(r =>
        {
            var requesterName = approvalUserSummaries.TryGetValue(r.RequesterId, out var req) ? req.DisplayName : "Requester";
            var currentStep = r.Steps.FirstOrDefault(s => s.StepOrder == r.CurrentStepOrder);

            return new ApprovalRequestSummaryDto(
                r.Id,
                r.RequestNumber,
                r.WorkflowDefinitionId,
                r.WorkflowDefinition?.Name ?? "Workflow",
                r.RequesterId,
                requesterName,
                r.Title,
                r.Status,
                r.CurrentStepOrder,
                currentStep?.Name,
                currentStep?.ApproverUserId,
                callerDisplayName,
                r.Version,
                r.SubmittedAtUtc,
                r.CompletedAtUtc
            );
        }).ToList();

        return new DashboardSummaryDto(
            assignedTasksCount,
            overdueTasksCount,
            openTicketsCount,
            pendingApprovalsCount,
            unreadNotificationsCount,
            managedProjectsCount,
            recentTasks,
            recentTickets,
            recentApprovals
        );
    }
}
