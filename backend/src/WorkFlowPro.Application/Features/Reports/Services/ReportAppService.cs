using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Reports.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Reports.Services;

public class ReportAppService : IReportAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    private static readonly string[] AgentRoles = { Roles.SupportAgent, Roles.Manager, Roles.Administrator };

    public ReportAppService(
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

    private bool IsAdmin() =>
        _currentUserService.Roles.Contains(Roles.Administrator, StringComparer.OrdinalIgnoreCase);

    private bool IsAgentOrAdmin() =>
        _currentUserService.Roles.Any(r => AgentRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<Guid>> GetAuthorizedProjectIdsAsync(CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        if (IsAdmin())
        {
            return await _context.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken);
        }

        return await _context.ProjectMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId)
            .Select(m => m.ProjectId)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskReportDto> GetTaskReportAsync(TaskReportRequest request, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(cancellationToken);

        if (request.ProjectId.HasValue)
        {
            if (!authorizedProjectIds.Contains(request.ProjectId.Value))
            {
                throw new KeyNotFoundException($"Project '{request.ProjectId.Value}' not found.");
            }
        }

        var query = _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .AsQueryable();

        if (request.ProjectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == request.ProjectId.Value);
        }
        else
        {
            query = query.Where(t => authorizedProjectIds.Contains(t.ProjectId));
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc >= request.StartDate.Value);
        }
        if (request.EndDate.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc <= request.EndDate.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Status) && TaskStatuses.IsValid(request.Status))
        {
            query = query.Where(t => t.Status == request.Status);
        }

        var now = DateTime.UtcNow;
        var allMatching = await query.ToListAsync(cancellationToken);

        var totalTasks = allMatching.Count;
        var completedTasks = allMatching.Count(t => t.Status == TaskStatuses.Done);
        var overdueTasks = allMatching.Count(t => t.Status != TaskStatuses.Done &&
                                                 t.Status != TaskStatuses.Cancelled &&
                                                 t.DueAtUtc.HasValue &&
                                                 t.DueAtUtc.Value < now);

        var completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 1) : 0.0;

        var statusDist = allMatching
            .GroupBy(t => t.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var priorityDist = allMatching
            .GroupBy(t => t.Priority)
            .ToDictionary(g => g.Key, g => g.Count());

        // Paged items
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalTasks / (double)pageSize);

        var pagedEntities = allMatching
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var assigneeIds = pagedEntities
            .Where(t => t.AssigneeId.HasValue)
            .Select(t => t.AssigneeId!.Value)
            .Distinct()
            .ToList();

        var userSummaries = await _identityService.GetUsersSummaryAsync(assigneeIds, cancellationToken);

        var items = pagedEntities.Select(t =>
        {
            string? assigneeName = null;
            if (t.AssigneeId.HasValue && userSummaries.TryGetValue(t.AssigneeId.Value, out var a))
            {
                assigneeName = a.DisplayName;
            }

            return new TaskReportItemDto(
                t.Id,
                t.Project?.ProjectCode ?? string.Empty,
                t.Project?.Name ?? string.Empty,
                t.Title,
                t.Status,
                t.Priority,
                assigneeName,
                t.DueAtUtc,
                t.CreatedAtUtc
            );
        }).ToList();

        var pagedResult = new PagedResult<TaskReportItemDto>(items, totalTasks, page, pageSize, totalPages);

        return new TaskReportDto(
            totalTasks,
            completedTasks,
            completionRate,
            overdueTasks,
            statusDist,
            priorityDist,
            pagedResult
        );
    }

    public async Task<CsvExportResult> ExportTaskReportCsvAsync(TaskReportRequest request, CancellationToken cancellationToken)
    {
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(cancellationToken);

        if (request.ProjectId.HasValue && !authorizedProjectIds.Contains(request.ProjectId.Value))
        {
            throw new KeyNotFoundException($"Project '{request.ProjectId.Value}' not found.");
        }

        var query = _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .AsQueryable();

        if (request.ProjectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == request.ProjectId.Value);
        }
        else
        {
            query = query.Where(t => authorizedProjectIds.Contains(t.ProjectId));
        }

        if (request.StartDate.HasValue) query = query.Where(t => t.CreatedAtUtc >= request.StartDate.Value);
        if (request.EndDate.HasValue) query = query.Where(t => t.CreatedAtUtc <= request.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(request.Status) && TaskStatuses.IsValid(request.Status))
        {
            query = query.Where(t => t.Status == request.Status);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(1000) // Bound exports to 1000 rows
            .ToListAsync(cancellationToken);

        var assigneeIds = tasks
            .Where(t => t.AssigneeId.HasValue)
            .Select(t => t.AssigneeId!.Value)
            .Distinct()
            .ToList();

        var userSummaries = await _identityService.GetUsersSummaryAsync(assigneeIds, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("TaskId,ProjectCode,ProjectName,Title,Status,Priority,Assignee,DueDateUtc,CreatedAtUtc");

        foreach (var t in tasks)
        {
            string? assigneeName = null;
            if (t.AssigneeId.HasValue && userSummaries.TryGetValue(t.AssigneeId.Value, out var a))
            {
                assigneeName = a.DisplayName;
            }

            sb.AppendLine(string.Join(",",
                EscapeCsv(t.Id.ToString()),
                EscapeCsv(t.Project?.ProjectCode ?? string.Empty),
                EscapeCsv(t.Project?.Name ?? string.Empty),
                EscapeCsv(t.Title),
                EscapeCsv(t.Status),
                EscapeCsv(t.Priority),
                EscapeCsv(assigneeName ?? "Unassigned"),
                EscapeCsv(t.DueAtUtc?.ToString("o") ?? string.Empty),
                EscapeCsv(t.CreatedAtUtc.ToString("o"))
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new CsvExportResult(bytes, "text/csv", $"task_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    public async Task<TicketReportDto> GetTicketReportAsync(TicketReportRequest request, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var query = _context.SupportTickets
            .AsNoTracking()
            .AsQueryable();

        if (!isAgent)
        {
            query = query.Where(t => t.RequesterId == callerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Category) && TicketCategories.IsValid(request.Category))
        {
            query = query.Where(t => t.Category == request.Category);
        }
        if (!string.IsNullOrWhiteSpace(request.Priority) && TicketPriorities.IsValid(request.Priority))
        {
            query = query.Where(t => t.Priority == request.Priority);
        }
        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc >= request.StartDate.Value);
        }
        if (request.EndDate.HasValue)
        {
            query = query.Where(t => t.CreatedAtUtc <= request.EndDate.Value);
        }

        var allMatching = await query.ToListAsync(cancellationToken);

        var totalTickets = allMatching.Count;
        var resolvedTickets = allMatching.Count(t => t.Status == TicketStatuses.Resolved || t.Status == TicketStatuses.Closed);
        var resolutionRate = totalTickets > 0 ? Math.Round((double)resolvedTickets / totalTickets * 100, 1) : 0.0;

        var categoryDist = allMatching
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var priorityDist = allMatching
            .GroupBy(t => t.Priority)
            .ToDictionary(g => g.Key, g => g.Count());

        var statusDist = allMatching
            .GroupBy(t => t.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalTickets / (double)pageSize);

        var pagedEntities = allMatching
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var userIds = pagedEntities
            .Select(t => t.RequesterId)
            .Concat(pagedEntities.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value))
            .Distinct()
            .ToList();

        var userSummaries = await _identityService.GetUsersSummaryAsync(userIds, cancellationToken);

        var items = pagedEntities.Select(t =>
        {
            var requesterName = userSummaries.TryGetValue(t.RequesterId, out var req) ? req.DisplayName : "Requester";
            string? assigneeName = null;
            if (t.AssigneeId.HasValue && userSummaries.TryGetValue(t.AssigneeId.Value, out var ass))
            {
                assigneeName = ass.DisplayName;
            }

            return new TicketReportItemDto(
                t.TicketNumber,
                t.Subject,
                t.Category,
                t.Priority,
                t.Status,
                requesterName,
                assigneeName,
                t.CreatedAtUtc,
                t.ResolvedAtUtc
            );
        }).ToList();

        var pagedResult = new PagedResult<TicketReportItemDto>(items, totalTickets, page, pageSize, totalPages);

        return new TicketReportDto(
            totalTickets,
            resolvedTickets,
            resolutionRate,
            categoryDist,
            priorityDist,
            statusDist,
            pagedResult
        );
    }

    public async Task<CsvExportResult> ExportTicketReportCsvAsync(TicketReportRequest request, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isAgent = IsAgentOrAdmin();

        var query = _context.SupportTickets
            .AsNoTracking()
            .AsQueryable();

        if (!isAgent)
        {
            query = query.Where(t => t.RequesterId == callerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Category) && TicketCategories.IsValid(request.Category))
        {
            query = query.Where(t => t.Category == request.Category);
        }
        if (!string.IsNullOrWhiteSpace(request.Priority) && TicketPriorities.IsValid(request.Priority))
        {
            query = query.Where(t => t.Priority == request.Priority);
        }
        if (request.StartDate.HasValue) query = query.Where(t => t.CreatedAtUtc >= request.StartDate.Value);
        if (request.EndDate.HasValue) query = query.Where(t => t.CreatedAtUtc <= request.EndDate.Value);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var userIds = tickets
            .Select(t => t.RequesterId)
            .Concat(tickets.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value))
            .Distinct()
            .ToList();

        var userSummaries = await _identityService.GetUsersSummaryAsync(userIds, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("TicketNumber,Subject,Category,Priority,Status,Requester,Assignee,CreatedAtUtc,ResolvedAtUtc");

        foreach (var t in tickets)
        {
            var requesterName = userSummaries.TryGetValue(t.RequesterId, out var req) ? req.DisplayName : "Requester";
            string? assigneeName = null;
            if (t.AssigneeId.HasValue && userSummaries.TryGetValue(t.AssigneeId.Value, out var ass))
            {
                assigneeName = ass.DisplayName;
            }

            sb.AppendLine(string.Join(",",
                EscapeCsv($"#{t.TicketNumber}"),
                EscapeCsv(t.Subject),
                EscapeCsv(t.Category),
                EscapeCsv(t.Priority),
                EscapeCsv(t.Status),
                EscapeCsv(requesterName),
                EscapeCsv(assigneeName ?? "Unassigned"),
                EscapeCsv(t.CreatedAtUtc.ToString("o")),
                EscapeCsv(t.ResolvedAtUtc?.ToString("o") ?? string.Empty)
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new CsvExportResult(bytes, "text/csv", $"ticket_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    public async Task<ProjectPerformanceReportDto> GetProjectPerformanceAsync(
        Guid projectId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(cancellationToken);
        if (!authorizedProjectIds.Contains(projectId))
        {
            throw new KeyNotFoundException($"Project '{projectId}' not found.");
        }

        var project = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project '{projectId}' not found.");
        }

        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if (startDate.HasValue) query = query.Where(t => t.CreatedAtUtc >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.CreatedAtUtc <= endDate.Value);

        var tasks = await query.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var totalTasks = tasks.Count;
        var completedTasks = tasks.Count(t => t.Status == TaskStatuses.Done);
        var overdueTasks = tasks.Count(t => t.Status != TaskStatuses.Done &&
                                           t.Status != TaskStatuses.Cancelled &&
                                           t.DueAtUtc.HasValue &&
                                           t.DueAtUtc.Value < now);

        var completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 1) : 0.0;

        return new ProjectPerformanceReportDto(
            project.Id,
            project.ProjectCode,
            project.Name,
            project.Status,
            project.StartDate,
            project.TargetDate,
            totalTasks,
            completedTasks,
            overdueTasks,
            completionRate,
            project.Members.Count
        );
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
