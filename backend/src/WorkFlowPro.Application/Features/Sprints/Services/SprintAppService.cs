using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Features.Sprints.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Sprints.Services;

public class SprintAppService : ISprintAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IValidator<CreateSprintRequest> _createValidator;
    private readonly IValidator<UpdateSprintRequest> _updateValidator;
    private readonly IValidator<StartSprintRequest> _startValidator;
    private readonly IValidator<UpdateBoardColumnConfigsRequest> _boardConfigValidator;
    private readonly IValidator<CreateIssueLinkRequest> _issueLinkValidator;

    public SprintAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        IValidator<CreateSprintRequest> createValidator,
        IValidator<UpdateSprintRequest> updateValidator,
        IValidator<StartSprintRequest> startValidator,
        IValidator<UpdateBoardColumnConfigsRequest> boardConfigValidator,
        IValidator<CreateIssueLinkRequest> issueLinkValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _startValidator = startValidator;
        _boardConfigValidator = boardConfigValidator;
        _issueLinkValidator = issueLinkValidator;
    }

    public async Task<IReadOnlyList<SprintDto>> GetProjectSprintsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.Status == SprintStatuses.Active)
            .ThenByDescending(s => s.Status == SprintStatuses.Planned)
            .ThenByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var sprintIds = sprints.Select(s => s.Id).ToList();
        var taskStats = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.SprintId.HasValue && sprintIds.Contains(t.SprintId.Value))
            .Select(t => new
            {
                SprintId = t.SprintId!.Value,
                t.Status,
                Points = t.StoryPoints ?? 0
            })
            .ToListAsync(cancellationToken);

        var statsBySprint = taskStats
            .GroupBy(t => t.SprintId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return sprints.Select(s =>
        {
            statsBySprint.TryGetValue(s.Id, out var items);
            var totalTasks = items?.Count ?? 0;
            var completedTasks = items?.Count(i => i.Status == TaskStatuses.Done) ?? 0;
            var totalPoints = items?.Sum(i => i.Points) ?? 0;
            var completedPoints = items?.Where(i => i.Status == TaskStatuses.Done).Sum(i => i.Points) ?? 0;

            return MapSprintDto(s, totalTasks, completedTasks, totalPoints, completedPoints);
        }).ToList();
    }

    public async Task<SprintDto> CreateSprintAsync(
        Guid projectId,
        CreateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageSprints(project, callerId);

        if (project.IsArchived)
        {
            throw new DomainException("Cannot create sprints in an archived project.");
        }

        var sprint = new Sprint(
            projectId: projectId,
            name: request.Name,
            createdById: callerId,
            goal: request.Goal,
            startDateUtc: request.StartDateUtc,
            endDateUtc: request.EndDateUtc);

        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.SprintCreated,
            "Sprint",
            sprint.Id,
            new { sprint.ProjectId, sprint.Name },
            cancellationToken);

        return MapSprintDto(sprint, 0, 0, 0, 0);
    }

    public async Task<SprintDto> UpdateSprintAsync(
        Guid sprintId,
        UpdateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var sprint = await GetAuthorizedSprintAsync(sprintId, callerId, cancellationToken);
        EnsureCanManageSprints(sprint.Project!, callerId);

        sprint.UpdateDetails(request.Name, request.Goal, request.StartDateUtc, request.EndDateUtc);
        await _context.SaveChangesAsync(cancellationToken);

        return await BuildSprintDtoAsync(sprint, cancellationToken);
    }

    public async Task<SprintDto> StartSprintAsync(
        Guid sprintId,
        StartSprintRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _startValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var sprint = await GetAuthorizedSprintAsync(sprintId, callerId, cancellationToken);
        EnsureCanManageSprints(sprint.Project!, callerId);

        if (sprint.Project!.IsArchived)
        {
            throw new DomainException("Cannot start sprints in an archived project.");
        }

        var hasOtherActive = await _context.Sprints
            .AnyAsync(s => s.ProjectId == sprint.ProjectId && s.Id != sprint.Id && s.Status == SprintStatuses.Active,
                cancellationToken);
        if (hasOtherActive)
        {
            throw new DomainException("This project already has an active sprint. Complete the active sprint before starting another.");
        }

        var now = DateTime.UtcNow;
        sprint.Start(request.StartDateUtc, request.EndDateUtc, now);
        await UpsertBurndownSnapshotInternalAsync(sprint.Id, now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.SprintStarted,
            "Sprint",
            sprint.Id,
            new { sprint.ProjectId, sprint.Name, sprint.StartDateUtc, sprint.EndDateUtc },
            cancellationToken);

        return await BuildSprintDtoAsync(sprint, cancellationToken);
    }

    public async Task<CompleteSprintResultDto> CompleteSprintAsync(
        Guid sprintId,
        CompleteSprintRequest request,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var sprint = await GetAuthorizedSprintAsync(sprintId, callerId, cancellationToken);
        EnsureCanManageSprints(sprint.Project!, callerId);

        if (request.MoveIncompleteToSprintId.HasValue)
        {
            if (request.MoveIncompleteToSprintId.Value == sprint.Id)
            {
                throw new DomainException("Cannot roll incomplete issues into the sprint being completed.");
            }

            var targetSprint = await _context.Sprints
                .FirstOrDefaultAsync(s => s.Id == request.MoveIncompleteToSprintId.Value && s.ProjectId == sprint.ProjectId,
                    cancellationToken);

            if (targetSprint == null || targetSprint.Status == SprintStatuses.Closed)
            {
                throw new DomainException("Target rollover sprint must be a non-closed sprint in the same project.");
            }
        }

        var now = DateTime.UtcNow;
        await UpsertBurndownSnapshotInternalAsync(sprint.Id, now, cancellationToken);

        var sprintTasks = await _context.Tasks
            .Where(t => t.SprintId == sprint.Id)
            .ToListAsync(cancellationToken);

        var completedCount = sprintTasks.Count(t => t.Status == TaskStatuses.Done);
        var incompleteTasks = sprintTasks
            .Where(t => t.Status != TaskStatuses.Done && t.Status != TaskStatuses.Cancelled)
            .ToList();

        foreach (var task in incompleteTasks)
        {
            task.AssignToSprint(request.MoveIncompleteToSprintId);
        }

        sprint.Complete(now);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.SprintCompleted,
            "Sprint",
            sprint.Id,
            new
            {
                sprint.ProjectId,
                sprint.Name,
                completedCount,
                RolledOverCount = incompleteTasks.Count,
                request.MoveIncompleteToSprintId
            },
            cancellationToken);

        var completedDto = MapSprintDto(
            sprint,
            totalTasks: completedCount,
            completedTasks: completedCount,
            totalPoints: sprintTasks.Where(t => t.Status == TaskStatuses.Done).Sum(t => t.StoryPoints ?? 0),
            completedPoints: sprintTasks.Where(t => t.Status == TaskStatuses.Done).Sum(t => t.StoryPoints ?? 0));

        return new CompleteSprintResultDto(
            CompletedSprint: completedDto,
            CompletedTasksCount: completedCount,
            RolledOverTasksCount: incompleteTasks.Count,
            RolledOverToSprintId: request.MoveIncompleteToSprintId);
    }

    public async Task<SprintBurndownDto> GetSprintBurndownAsync(
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var sprint = await GetAuthorizedSprintAsync(sprintId, callerId, cancellationToken);

        var snapshots = await _context.SprintBurndownSnapshots
            .AsNoTracking()
            .Where(s => s.SprintId == sprintId)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        var totalPoints = snapshots.FirstOrDefault()?.TotalPoints
            ?? await _context.Tasks
                .AsNoTracking()
                .Where(t => t.SprintId == sprintId)
                .SumAsync(t => t.StoryPoints ?? 0, cancellationToken);

        var startDate = DateOnly.FromDateTime((sprint.StartDateUtc ?? sprint.StartedAtUtc ?? sprint.CreatedAtUtc).Date);
        var endDate = DateOnly.FromDateTime((sprint.EndDateUtc ?? sprint.CompletedAtUtc ?? DateTime.UtcNow.AddDays(14)).Date);
        var totalDays = Math.Max(1, endDate.DayNumber - startDate.DayNumber);

        var snapshotMap = snapshots.ToDictionary(s => s.SnapshotDate, s => s);
        var points = new List<SprintBurndownPointDto>();

        var lastActualRemaining = totalPoints;
        var lastCompleted = 0;
        var lastRemainingCount = snapshots.FirstOrDefault()?.RemainingTaskCount ?? 0;

        var maxRenderDate = sprint.Status == SprintStatuses.Closed && sprint.CompletedAtUtc.HasValue
            ? DateOnly.FromDateTime(sprint.CompletedAtUtc.Value.Date)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (maxRenderDate < startDate) maxRenderDate = startDate;
        if (endDate > maxRenderDate && snapshots.Count > 0)
        {
            // Render timeline across planned sprint days up to endDate for ideal line
        }

        var horizonEnd = endDate >= startDate ? endDate : startDate;
        for (var day = 0; day <= totalDays; day++)
        {
            var currentDate = startDate.AddDays(day);
            var ideal = (int)Math.Round(totalPoints * (1.0 - (double)day / totalDays));

            if (snapshotMap.TryGetValue(currentDate, out var snap))
            {
                lastActualRemaining = snap.RemainingPoints;
                lastCompleted = snap.CompletedPoints;
                lastRemainingCount = snap.RemainingTaskCount;
            }

            points.Add(new SprintBurndownPointDto(
                Date: currentDate,
                IdealRemainingPoints: Math.Max(0, ideal),
                ActualRemainingPoints: lastActualRemaining,
                CompletedPoints: lastCompleted,
                RemainingTaskCount: lastRemainingCount));
        }

        return new SprintBurndownDto(
            SprintId: sprint.Id,
            SprintName: sprint.Name,
            Status: sprint.Status,
            StartDateUtc: sprint.StartDateUtc,
            EndDateUtc: sprint.EndDateUtc,
            TotalPoints: totalPoints,
            Points: points);
    }

    public async Task<VelocityChartDto> GetProjectVelocityAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        var closedSprints = await _context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.Status == SprintStatuses.Closed)
            .OrderByDescending(s => s.CompletedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);

        closedSprints.Reverse();

        var bars = new List<VelocitySprintBarDto>();
        foreach (var sprint in closedSprints)
        {
            var finalSnap = await _context.SprintBurndownSnapshots
                .AsNoTracking()
                .Where(sn => sn.SprintId == sprint.Id)
                .OrderByDescending(sn => sn.SnapshotDate)
                .FirstOrDefaultAsync(cancellationToken);

            var doneTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.SprintId == sprint.Id && t.Status == TaskStatuses.Done)
                .Select(t => new { Points = t.StoryPoints ?? 0 })
                .ToListAsync(cancellationToken);

            var completedPoints = finalSnap?.CompletedPoints ?? doneTasks.Sum(t => t.Points);
            var committedPoints = finalSnap?.TotalPoints ?? completedPoints;

            bars.Add(new VelocitySprintBarDto(
                SprintId: sprint.Id,
                SprintName: sprint.Name,
                CommittedPoints: committedPoints,
                CompletedPoints: completedPoints,
                CompletedTasks: doneTasks.Count,
                CompletedAtUtc: sprint.CompletedAtUtc));
        }

        var avg = bars.Count > 0 ? Math.Round(bars.Average(b => b.CompletedPoints), 1) : 0.0;
        return new VelocityChartDto(projectId, avg, bars);
    }

    public async Task<IReadOnlyList<BoardColumnConfigDto>> GetBoardConfigsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        var saved = await _context.BoardColumnConfigs
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (saved.Count == 0)
        {
            return TaskStatuses.All
                .Select((status, idx) => new BoardColumnConfigDto(status, idx, null, false, true))
                .ToList();
        }

        var byStatus = saved.ToDictionary(c => c.Status, StringComparer.OrdinalIgnoreCase);
        var result = new List<BoardColumnConfigDto>();
        for (var i = 0; i < TaskStatuses.All.Count; i++)
        {
            var status = TaskStatuses.All[i];
            if (byStatus.TryGetValue(status, out var cfg))
            {
                result.Add(new BoardColumnConfigDto(cfg.Status, cfg.DisplayOrder, cfg.WipLimit, cfg.IsStrictWip, cfg.IsVisible));
            }
            else
            {
                result.Add(new BoardColumnConfigDto(status, i, null, false, true));
            }
        }

        return result.OrderBy(c => c.DisplayOrder).ToList();
    }

    public async Task<IReadOnlyList<BoardColumnConfigDto>> UpdateBoardConfigsAsync(
        Guid projectId,
        UpdateBoardColumnConfigsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _boardConfigValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageSprints(project, callerId);

        var existing = await _context.BoardColumnConfigs
            .Where(c => c.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var byStatus = existing.ToDictionary(c => c.Status, StringComparer.OrdinalIgnoreCase);

        foreach (var col in request.Columns)
        {
            var normalizedStatus = TaskStatuses.All.First(s => string.Equals(s, col.Status, StringComparison.OrdinalIgnoreCase));
            if (byStatus.TryGetValue(normalizedStatus, out var cfg))
            {
                cfg.Update(col.DisplayOrder, col.WipLimit, col.IsStrictWip, col.IsVisible);
            }
            else
            {
                _context.BoardColumnConfigs.Add(new ProjectBoardColumnConfig(
                    projectId, normalizedStatus, col.DisplayOrder, col.WipLimit, col.IsStrictWip, col.IsVisible));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.BoardConfigUpdated,
            "Project",
            projectId,
            new { projectId, ColumnCount = request.Columns.Count },
            cancellationToken);

        return await GetBoardConfigsAsync(projectId, cancellationToken);
    }

    public async Task<IssueLinkDto> CreateIssueLinkAsync(
        Guid sourceTaskId,
        CreateIssueLinkRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _issueLinkValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var sourceTask = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .FirstOrDefaultAsync(t => t.Id == sourceTaskId, cancellationToken);

        if (sourceTask == null || sourceTask.Project == null)
        {
            throw new KeyNotFoundException($"Task {sourceTaskId} not found.");
        }
        EnsureUserCanAccessProject(sourceTask.Project, callerId);

        var targetTask = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .FirstOrDefaultAsync(t => t.Id == request.TargetTaskId, cancellationToken);

        if (targetTask == null || targetTask.Project == null)
        {
            throw new KeyNotFoundException($"Target task {request.TargetTaskId} not found.");
        }
        EnsureUserCanAccessProject(targetTask.Project, callerId);

        var normalizedType = IssueLinkTypes.Normalize(request.LinkType);
        var exists = await _context.IssueLinks
            .AnyAsync(l => l.SourceTaskId == sourceTaskId && l.TargetTaskId == request.TargetTaskId && l.LinkType == normalizedType,
                cancellationToken);
        if (exists)
        {
            throw new DomainException($"Link '{normalizedType}' to {targetTask.TaskKey} already exists.");
        }

        if (normalizedType == IssueLinkTypes.Blocks)
        {
            var createsCycle = await WouldCreateBlockingCycleAsync(sourceTaskId, request.TargetTaskId, cancellationToken);
            if (createsCycle)
            {
                throw new DomainException("Cannot add link because it would create a circular blocking dependency.");
            }
        }

        var link = new IssueLink(sourceTaskId, request.TargetTaskId, normalizedType, callerId);
        _context.IssueLinks.Add(link);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.IssueLinkCreated,
            "IssueLink",
            link.Id,
            new { sourceTaskId, request.TargetTaskId, LinkType = normalizedType },
            cancellationToken);

        return new IssueLinkDto(
            Id: link.Id,
            LinkType: link.LinkType,
            DisplayRelation: link.LinkType.ToLowerInvariant(),
            RelatedTaskId: targetTask.Id,
            RelatedTaskKey: targetTask.TaskKey,
            RelatedTaskTitle: targetTask.Title,
            RelatedTaskStatus: targetTask.Status,
            RelatedTaskIssueType: targetTask.IssueType,
            CreatedAtUtc: link.CreatedAtUtc);
    }

    public async Task DeleteIssueLinkAsync(
        Guid taskId,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var link = await _context.IssueLinks
            .FirstOrDefaultAsync(l => l.Id == linkId && (l.SourceTaskId == taskId || l.TargetTaskId == taskId),
                cancellationToken);
        if (link == null)
        {
            throw new KeyNotFoundException($"Issue link {linkId} not found.");
        }

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task?.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }
        EnsureUserCanAccessProject(task.Project, callerId);

        _context.IssueLinks.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.IssueLinkDeleted,
            "IssueLink",
            linkId,
            new { taskId, linkId },
            cancellationToken);
    }

    public async Task RecordActiveSprintSnapshotAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        var sprint = await _context.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken);

        if (sprint == null || sprint.Status != SprintStatuses.Active)
        {
            return;
        }

        await UpsertBurndownSnapshotInternalAsync(sprintId, DateTime.UtcNow, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertBurndownSnapshotInternalAsync(
        Guid sprintId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.SprintId == sprintId && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.Status, Points = t.StoryPoints ?? 0 })
            .ToListAsync(cancellationToken);

        var totalPoints = tasks.Sum(t => t.Points);
        var completedPoints = tasks.Where(t => t.Status == TaskStatuses.Done).Sum(t => t.Points);
        var remainingPoints = totalPoints - completedPoints;
        var remainingCount = tasks.Count(t => t.Status != TaskStatuses.Done);
        var today = DateOnly.FromDateTime(now.Date);

        var existing = await _context.SprintBurndownSnapshots
            .FirstOrDefaultAsync(s => s.SprintId == sprintId && s.SnapshotDate == today, cancellationToken);

        if (existing == null)
        {
            _context.SprintBurndownSnapshots.Add(new SprintBurndownSnapshot(
                sprintId, today, totalPoints, remainingPoints, completedPoints, remainingCount, now));
        }
        else
        {
            existing.UpdateCounts(totalPoints, remainingPoints, completedPoints, remainingCount, now);
        }
    }

    private async Task<bool> WouldCreateBlockingCycleAsync(
        Guid sourceTaskId,
        Guid targetTaskId,
        CancellationToken cancellationToken)
    {
        // Check if targetTaskId already (transitively) blocks sourceTaskId
        var blockingEdges = await _context.IssueLinks
            .AsNoTracking()
            .Where(l => l.LinkType == IssueLinkTypes.Blocks)
            .Select(l => new { l.SourceTaskId, l.TargetTaskId })
            .ToListAsync(cancellationToken);

        var adjacency = blockingEdges
            .GroupBy(e => e.SourceTaskId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TargetTaskId).ToList());

        var visited = new HashSet<Guid> { targetTaskId };
        var queue = new Queue<Guid>();
        queue.Enqueue(targetTaskId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == sourceTaskId) return true;

            if (adjacency.TryGetValue(current, out var nexts))
            {
                foreach (var next in nexts)
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return false;
    }

    private async Task<SprintDto> BuildSprintDtoAsync(Sprint sprint, CancellationToken cancellationToken)
    {
        var items = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.SprintId == sprint.Id)
            .Select(t => new { t.Status, Points = t.StoryPoints ?? 0 })
            .ToListAsync(cancellationToken);

        return MapSprintDto(
            sprint,
            items.Count,
            items.Count(i => i.Status == TaskStatuses.Done),
            items.Sum(i => i.Points),
            items.Where(i => i.Status == TaskStatuses.Done).Sum(i => i.Points));
    }

    private static SprintDto MapSprintDto(
        Sprint s,
        int totalTasks,
        int completedTasks,
        int totalPoints,
        int completedPoints) =>
        new(
            Id: s.Id,
            ProjectId: s.ProjectId,
            Name: s.Name,
            Goal: s.Goal,
            Status: s.Status,
            StartDateUtc: s.StartDateUtc,
            EndDateUtc: s.EndDateUtc,
            StartedAtUtc: s.StartedAtUtc,
            CompletedAtUtc: s.CompletedAtUtc,
            TotalTasks: totalTasks,
            CompletedTasks: completedTasks,
            TotalPoints: totalPoints,
            CompletedPoints: completedPoints,
            CreatedAtUtc: s.CreatedAtUtc);

    private async Task<Project> GetAuthorizedProjectAsync(
        Guid projectId,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null) throw new KeyNotFoundException($"Project {projectId} not found.");
        EnsureUserCanAccessProject(project, callerId);
        return project;
    }

    private async Task<Sprint> GetAuthorizedSprintAsync(
        Guid sprintId,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        var sprint = await _context.Sprints
            .Include(s => s.Project)
                .ThenInclude(p => p!.Members)
            .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken);

        if (sprint?.Project == null) throw new KeyNotFoundException($"Sprint {sprintId} not found.");
        EnsureUserCanAccessProject(sprint.Project, callerId);
        return sprint;
    }

    private void EnsureUserCanAccessProject(Project project, Guid callerId)
    {
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);
        var isMember = project.Members.Any(m => m.UserId == callerId);
        if (!isAdmin && !isMember)
        {
            throw new KeyNotFoundException($"Project {project.Id} not found.");
        }
    }

    private void EnsureCanManageSprints(Project project, Guid callerId)
    {
        if (_currentUserService.Roles.Contains(Roles.Administrator)) return;

        var member = project.Members.FirstOrDefault(m => m.UserId == callerId);
        if (member == null ||
            (member.ProjectRole != ProjectRoles.Owner && member.ProjectRole != ProjectRoles.Manager))
        {
            throw new ForbiddenException("Only Project Owners, Project Managers, or Administrators can manage sprints and board settings.");
        }
    }

    private Guid RequireCallerId()
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }
        return _currentUserService.UserId.Value;
    }
}
