using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Application.Features.Tasks.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Tasks.Services;

public class TaskAppService : ITaskAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<ChangeTaskStatusRequest> _statusValidator;
    private readonly IValidator<AddTaskCommentRequest> _commentValidator;

    public TaskAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskRequest> updateValidator,
        IValidator<ChangeTaskStatusRequest> statusValidator,
        IValidator<AddTaskCommentRequest> commentValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _commentValidator = commentValidator;
    }

    public async Task<TaskSummaryDto> CreateAsync(
        Guid projectId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        if (project.IsArchived)
        {
            throw new DomainException("Cannot create tasks in an archived project.");
        }

        if (request.AssigneeId.HasValue)
        {
            await ValidateAssigneeAsync(project, request.AssigneeId.Value, cancellationToken);
        }

        var initialStatus = !string.IsNullOrWhiteSpace(request.Status)
            ? request.Status
            : TaskStatuses.Backlog;

        var task = new TaskItem(
            projectId: projectId,
            title: request.Title,
            description: request.Description,
            createdById: callerId,
            priority: request.Priority,
            assigneeId: request.AssigneeId,
            dueAtUtc: request.DueAtUtc,
            estimateMinutes: request.EstimateMinutes,
            status: initialStatus
        );

        var maxTaskNumber = await _context.Tasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => (int?)t.TaskNumber)
            .MaxAsync(cancellationToken) ?? 0;

        task.AssignTaskKey(maxTaskNumber + 1, project.ProjectCode);

        _context.Tasks.Add(task);

        if (task.AssigneeId.HasValue && task.AssigneeId.Value != callerId)
        {
            _context.Notifications.Add(new InAppNotification(
                task.AssigneeId.Value,
                NotificationTypes.TaskAssigned,
                $"Assigned to Task: {task.Title}",
                $"You have been assigned to task '{task.Title}' in {project.ProjectCode}.",
                "Task",
                task.Id
            ));
        }

        await _context.SaveChangesAsync(cancellationToken);

        var userMap = await GetUserMapAsync(new[] { callerId, request.AssigneeId ?? Guid.Empty }, cancellationToken);

        return MapToSummary(task, project, userMap);
    }

    public async Task<PagedResult<TaskSummaryDto>> GetProjectTasksAsync(
        Guid projectId,
        string? status,
        string? priority,
        Guid? assigneeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        string? prState = null)
    {
        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (assigneeId.HasValue)
        {
            query = query.Where(t => t.AssigneeId == assigneeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(prState) && !string.Equals(prState, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(prState, "HasPr", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.PullRequestLinks.Any());
            }
            else if (string.Equals(prState, "ChecksFailing", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.PullRequestLinks.Any(l =>
                    l.PullRequest != null && l.PullRequest.CheckStatus == PullRequestCheckStatuses.Failure));
            }
            else
            {
                query = query.Where(t => t.PullRequestLinks.Any(l =>
                    l.PullRequest != null && l.PullRequest.State == prState));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                t.TaskKey.ToLower().Contains(term) ||
                t.Title.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var tasks = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.ProjectId,
                t.TaskNumber,
                t.TaskKey,
                t.Title,
                t.Status,
                t.Priority,
                t.AssigneeId,
                t.CreatedById,
                t.DueAtUtc,
                t.EstimateMinutes,
                t.Version,
                CommentCount = t.Comments.Count,
                t.CreatedAtUtc,
                t.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var userIds = tasks.Select(t => t.CreatedById)
            .Concat(tasks.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value))
            .Distinct();

        var userMap = await GetUserMapAsync(userIds, cancellationToken);
        var gitSummaryMap = await BuildGitSummaryMapAsync(tasks.Select(t => t.Id).ToList(), cancellationToken);

        var items = tasks.Select(t => new TaskSummaryDto(
            Id: t.Id,
            ProjectId: t.ProjectId,
            ProjectCode: project.ProjectCode,
            ProjectName: project.Name,
            Title: t.Title,
            Status: t.Status,
            Priority: t.Priority,
            AssigneeId: t.AssigneeId,
            AssigneeName: t.AssigneeId.HasValue && userMap.TryGetValue(t.AssigneeId.Value, out var a) ? a.DisplayName : null,
            CreatedById: t.CreatedById,
            CreatedByName: userMap.TryGetValue(t.CreatedById, out var c) ? c.DisplayName : "Unknown",
            DueAtUtc: t.DueAtUtc,
            EstimateMinutes: t.EstimateMinutes,
            Version: t.Version,
            CommentCount: t.CommentCount,
            CreatedAtUtc: t.CreatedAtUtc,
            UpdatedAtUtc: t.UpdatedAtUtc,
            TaskNumber: t.TaskNumber,
            TaskKey: t.TaskKey,
            GitSummary: gitSummaryMap.TryGetValue(t.Id, out var gs) ? gs : null
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<TaskSummaryDto>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<TaskDetailDto> GetByIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .Include(t => t.Comments)
            .Include(t => t.StatusHistory)
            .Include(t => t.GitBranches)
                .ThenInclude(b => b.Repository)
            .Include(t => t.PullRequestLinks)
                .ThenInclude(l => l.PullRequest)
                    .ThenInclude(pr => pr!.Repository)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null || task.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }

        EnsureUserCanAccessProject(task.Project, callerId);

        var userIds = new List<Guid> { task.CreatedById };
        if (task.AssigneeId.HasValue) userIds.Add(task.AssigneeId.Value);
        userIds.AddRange(task.Comments.Select(c => c.AuthorId));
        userIds.AddRange(task.StatusHistory.Select(h => h.ChangedById));

        var userMap = await GetUserMapAsync(userIds.Distinct(), cancellationToken);

        var comments = task.Comments
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new TaskCommentDto(
                Id: c.Id,
                TaskId: c.TaskId,
                AuthorId: c.AuthorId,
                AuthorName: userMap.TryGetValue(c.AuthorId, out var author) ? author.DisplayName : "Unknown",
                Body: c.Body,
                CreatedAtUtc: c.CreatedAtUtc
            )).ToList();

        var history = task.StatusHistory
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new TaskStatusHistoryDto(
                Id: h.Id,
                TaskId: h.TaskId,
                FromStatus: h.FromStatus,
                ToStatus: h.ToStatus,
                ChangedById: h.ChangedById,
                ChangedByName: userMap.TryGetValue(h.ChangedById, out var changer) ? changer.DisplayName : "Unknown",
                ChangedAtUtc: h.ChangedAtUtc,
                Comment: h.Comment
            )).ToList();

        var branches = task.GitBranches
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new TaskGitBranchDto(
                Id: b.Id,
                TaskId: b.TaskId,
                RepositoryId: b.RepositoryId,
                RepositoryFullName: b.Repository?.RepositoryFullName ?? string.Empty,
                BranchName: b.BranchName,
                BaseBranch: b.BaseBranch,
                BranchUrl: b.BranchUrl,
                CreatedByUserId: b.CreatedByUserId,
                CreatedAtUtc: b.CreatedAtUtc
            )).ToList();

        var pullRequests = task.PullRequestLinks
            .Where(l => l.PullRequest != null)
            .OrderByDescending(l => l.PullRequest!.OpenedAtUtc)
            .Select(l =>
            {
                var pr = l.PullRequest!;
                return new TaskPullRequestDto(
                    Id: pr.Id,
                    RepositoryId: pr.RepositoryId,
                    RepositoryFullName: pr.Repository?.RepositoryFullName ?? string.Empty,
                    PrNumber: pr.PrNumber,
                    Title: pr.Title,
                    State: pr.State,
                    SourceBranch: pr.SourceBranch,
                    TargetBranch: pr.TargetBranch,
                    AuthorLogin: pr.AuthorLogin,
                    AuthorAvatarUrl: pr.AuthorAvatarUrl,
                    HtmlUrl: pr.HtmlUrl,
                    ReviewStatus: pr.ReviewStatus,
                    CheckStatus: pr.CheckStatus,
                    CommitsCount: pr.CommitsCount,
                    Additions: pr.Additions,
                    Deletions: pr.Deletions,
                    LinkSource: l.LinkSource,
                    OpenedAtUtc: pr.OpenedAtUtc,
                    MergedAtUtc: pr.MergedAtUtc,
                    ClosedAtUtc: pr.ClosedAtUtc,
                    LastSyncedAtUtc: pr.LastSyncedAtUtc
                );
            }).ToList();

        return new TaskDetailDto(
            Id: task.Id,
            ProjectId: task.ProjectId,
            ProjectCode: task.Project.ProjectCode,
            ProjectName: task.Project.Name,
            Title: task.Title,
            Description: task.Description,
            Status: task.Status,
            Priority: task.Priority,
            AssigneeId: task.AssigneeId,
            AssigneeName: task.AssigneeId.HasValue && userMap.TryGetValue(task.AssigneeId.Value, out var a) ? a.DisplayName : null,
            CreatedById: task.CreatedById,
            CreatedByName: userMap.TryGetValue(task.CreatedById, out var c) ? c.DisplayName : "Unknown",
            DueAtUtc: task.DueAtUtc,
            EstimateMinutes: task.EstimateMinutes,
            Version: task.Version,
            CreatedAtUtc: task.CreatedAtUtc,
            UpdatedAtUtc: task.UpdatedAtUtc,
            Comments: comments,
            History: history,
            TaskNumber: task.TaskNumber,
            TaskKey: task.TaskKey,
            Branches: branches,
            PullRequests: pullRequests
        );
    }

    public async Task<TaskSummaryDto> UpdateAsync(
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null || task.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }

        EnsureUserCanAccessProject(task.Project, callerId);

        if (task.Project.IsArchived)
        {
            throw new DomainException("Cannot modify tasks in an archived project.");
        }

        if (request.AssigneeId.HasValue)
        {
            await ValidateAssigneeAsync(task.Project, request.AssigneeId.Value, cancellationToken);
        }

        task.UpdateDetails(
            title: request.Title,
            description: request.Description,
            priority: request.Priority,
            assigneeId: request.AssigneeId,
            dueAtUtc: request.DueAtUtc,
            estimateMinutes: request.EstimateMinutes,
            expectedVersion: request.Version
        );

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Task has been modified by another operation. Please refresh.", ex);
        }

        var userMap = await GetUserMapAsync(new[] { task.CreatedById, task.AssigneeId ?? Guid.Empty }, cancellationToken);
        var gitSummaryMap = await BuildGitSummaryMapAsync(new[] { task.Id }, cancellationToken);
        return MapToSummary(task, task.Project, userMap, gitSummaryMap.GetValueOrDefault(task.Id));
    }

    public async Task<TaskSummaryDto> ChangeStatusAsync(
        Guid taskId,
        ChangeTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null || task.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }

        EnsureUserCanAccessProject(task.Project, callerId);

        if (task.Project.IsArchived)
        {
            throw new DomainException("Cannot modify tasks in an archived project.");
        }

        var history = task.ChangeStatus(
            newStatus: request.Status,
            changedById: callerId,
            expectedVersion: request.Version,
            comment: request.Comment
        );
        _context.TaskStatusHistories.Add(history);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Task has been modified by another operation. Please refresh.", ex);
        }

        var userMap = await GetUserMapAsync(new[] { task.CreatedById, task.AssigneeId ?? Guid.Empty }, cancellationToken);
        var gitSummaryMap = await BuildGitSummaryMapAsync(new[] { task.Id }, cancellationToken);
        return MapToSummary(task, task.Project, userMap, gitSummaryMap.GetValueOrDefault(task.Id));
    }

    public async Task<TaskCommentDto> AddCommentAsync(
        Guid taskId,
        AddTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _commentValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var callerId = RequireCallerId();

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null || task.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }

        EnsureUserCanAccessProject(task.Project, callerId);

        if (task.Project.IsArchived)
        {
            throw new DomainException("Cannot add comments in an archived project.");
        }

        var comment = task.AddComment(callerId, request.Body);
        _context.TaskComments.Add(comment);

        if (task.AssigneeId.HasValue && task.AssigneeId.Value != callerId)
        {
            _context.Notifications.Add(new InAppNotification(
                task.AssigneeId.Value,
                NotificationTypes.TaskCommentAdded,
                $"New Comment on Task: {task.Title}",
                $"A new comment was posted on task '{task.Title}'.",
                "Task",
                task.Id
            ));
        }

        await _context.SaveChangesAsync(cancellationToken);

        var callerUser = await _identityService.GetUserByIdAsync(callerId, cancellationToken);

        return new TaskCommentDto(
            Id: comment.Id,
            TaskId: comment.TaskId,
            AuthorId: comment.AuthorId,
            AuthorName: callerUser?.DisplayName ?? "Unknown",
            Body: comment.Body,
            CreatedAtUtc: comment.CreatedAtUtc
        );
    }

    public async Task<IReadOnlyList<TaskCommentDto>> GetCommentsAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null || task.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }

        EnsureUserCanAccessProject(task.Project, callerId);

        var authorIds = task.Comments.Select(c => c.AuthorId).Distinct();
        var userMap = await GetUserMapAsync(authorIds, cancellationToken);

        return task.Comments
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new TaskCommentDto(
                Id: c.Id,
                TaskId: c.TaskId,
                AuthorId: c.AuthorId,
                AuthorName: userMap.TryGetValue(c.AuthorId, out var author) ? author.DisplayName : "Unknown",
                Body: c.Body,
                CreatedAtUtc: c.CreatedAtUtc
            )).ToList();
    }

    public async Task<IReadOnlyList<TaskStatusHistoryDto>> GetHistoryAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.Members)
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null || task.Project == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found.");
        }

        EnsureUserCanAccessProject(task.Project, callerId);

        var changedByIds = task.StatusHistory.Select(h => h.ChangedById).Distinct();
        var userMap = await GetUserMapAsync(changedByIds, cancellationToken);

        return task.StatusHistory
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new TaskStatusHistoryDto(
                Id: h.Id,
                TaskId: h.TaskId,
                FromStatus: h.FromStatus,
                ToStatus: h.ToStatus,
                ChangedById: h.ChangedById,
                ChangedByName: userMap.TryGetValue(h.ChangedById, out var changer) ? changer.DisplayName : "Unknown",
                ChangedAtUtc: h.ChangedAtUtc,
                Comment: h.Comment
            )).ToList();
    }

    private async Task<Project> GetAuthorizedProjectAsync(
        Guid projectId,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found.");
        }

        EnsureUserCanAccessProject(project, callerId);
        return project;
    }

    private void EnsureUserCanAccessProject(Project project, Guid callerId)
    {
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator);
        var isMember = project.Members.Any(m => m.UserId == callerId);

        if (!isAdmin && !isMember)
        {
            // Resource concealment: return not found rather than revealing private project existence
            throw new KeyNotFoundException($"Project {project.Id} not found.");
        }
    }

    private async Task ValidateAssigneeAsync(Project project, Guid assigneeId, CancellationToken cancellationToken)
    {
        var isMember = project.Members.Any(m => m.UserId == assigneeId);
        if (!isMember)
        {
            throw new DomainException("Assignee must be an active project member.");
        }

        var isActive = await _identityService.IsUserActiveAsync(assigneeId, cancellationToken);
        if (!isActive)
        {
            throw new DomainException("Cannot assign task to an inactive user.");
        }
    }

    private async Task<IDictionary<Guid, (string DisplayName, string Email)>> GetUserMapAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var validIds = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        return await _identityService.GetUsersSummaryAsync(validIds, cancellationToken);
    }

    private async Task<Dictionary<Guid, TaskGitSummaryDto>> BuildGitSummaryMapAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, TaskGitSummaryDto>();
        }

        var branches = await _context.TaskGitBranches
            .AsNoTracking()
            .Where(b => taskIds.Contains(b.TaskId))
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new { b.TaskId, b.BranchName, b.BranchUrl })
            .ToListAsync(cancellationToken);

        var prLinks = await _context.TaskPullRequestLinks
            .AsNoTracking()
            .Where(l => taskIds.Contains(l.TaskId) && l.PullRequest != null)
            .Select(l => new
            {
                l.TaskId,
                l.PullRequest!.PrNumber,
                l.PullRequest.State,
                l.PullRequest.ReviewStatus,
                l.PullRequest.CheckStatus,
                l.PullRequest.HtmlUrl,
                l.PullRequest.OpenedAtUtc
            })
            .ToListAsync(cancellationToken);

        var branchesByTask = branches.GroupBy(b => b.TaskId).ToDictionary(g => g.Key, g => g.ToList());
        var prsByTask = prLinks.GroupBy(p => p.TaskId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OpenedAtUtc).ToList());

        var map = new Dictionary<Guid, TaskGitSummaryDto>();
        foreach (var taskId in taskIds)
        {
            branchesByTask.TryGetValue(taskId, out var taskBranches);
            prsByTask.TryGetValue(taskId, out var taskPrs);

            if ((taskBranches == null || taskBranches.Count == 0) && (taskPrs == null || taskPrs.Count == 0))
            {
                continue;
            }

            var primaryPr = taskPrs?.FirstOrDefault(p => p.State == PullRequestStates.Open || p.State == PullRequestStates.Draft)
                ?? taskPrs?.FirstOrDefault();
            var primaryBranch = taskBranches?.FirstOrDefault();

            map[taskId] = new TaskGitSummaryDto(
                BranchCount: taskBranches?.Count ?? 0,
                OpenPrCount: taskPrs?.Count(p => p.State == PullRequestStates.Open || p.State == PullRequestStates.Draft) ?? 0,
                MergedPrCount: taskPrs?.Count(p => p.State == PullRequestStates.Merged) ?? 0,
                PrimaryPrNumber: primaryPr?.PrNumber,
                PrimaryPrState: primaryPr?.State,
                PrimaryPrReviewStatus: primaryPr?.ReviewStatus,
                PrimaryPrCheckStatus: primaryPr?.CheckStatus,
                PrimaryPrUrl: primaryPr?.HtmlUrl,
                PrimaryBranchName: primaryBranch?.BranchName,
                PrimaryBranchUrl: primaryBranch?.BranchUrl
            );
        }

        return map;
    }

    private static TaskSummaryDto MapToSummary(
        TaskItem task,
        Project project,
        IDictionary<Guid, (string DisplayName, string Email)> userMap,
        TaskGitSummaryDto? gitSummary = null)
    {
        return new TaskSummaryDto(
            Id: task.Id,
            ProjectId: task.ProjectId,
            ProjectCode: project.ProjectCode,
            ProjectName: project.Name,
            Title: task.Title,
            Status: task.Status,
            Priority: task.Priority,
            AssigneeId: task.AssigneeId,
            AssigneeName: task.AssigneeId.HasValue && userMap.TryGetValue(task.AssigneeId.Value, out var a) ? a.DisplayName : null,
            CreatedById: task.CreatedById,
            CreatedByName: userMap.TryGetValue(task.CreatedById, out var c) ? c.DisplayName : "Unknown",
            DueAtUtc: task.DueAtUtc,
            EstimateMinutes: task.EstimateMinutes,
            Version: task.Version,
            CommentCount: task.Comments.Count,
            CreatedAtUtc: task.CreatedAtUtc,
            UpdatedAtUtc: task.UpdatedAtUtc,
            TaskNumber: task.TaskNumber,
            TaskKey: task.TaskKey,
            GitSummary: gitSummary
        );
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
