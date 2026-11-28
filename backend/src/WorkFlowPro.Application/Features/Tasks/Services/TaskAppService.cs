using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Application.Features.Sprints.Dtos;
using WorkFlowPro.Application.Features.Sprints.Services;
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
    private readonly ISprintAppService _sprintService;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<ChangeTaskStatusRequest> _statusValidator;
    private readonly IValidator<AddTaskCommentRequest> _commentValidator;
    private readonly IValidator<MoveTaskOnBoardRequest> _moveValidator;

    public TaskAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        ISprintAppService sprintService,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskRequest> updateValidator,
        IValidator<ChangeTaskStatusRequest> statusValidator,
        IValidator<AddTaskCommentRequest> commentValidator,
        IValidator<MoveTaskOnBoardRequest> moveValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _sprintService = sprintService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _commentValidator = commentValidator;
        _moveValidator = moveValidator;
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

        await ValidateAgileReferencesAsync(
            projectId,
            request.IssueType,
            request.EpicId,
            request.ParentTaskId,
            request.SprintId,
            selfId: Guid.Empty,
            cancellationToken);

        var maxRank = await _context.Tasks
            .Where(t => t.ProjectId == projectId && t.Status == initialStatus)
            .Select(t => (int?)t.BoardRank)
            .MaxAsync(cancellationToken) ?? 0;

        var task = new TaskItem(
            projectId: projectId,
            title: request.Title,
            description: request.Description,
            createdById: callerId,
            priority: request.Priority,
            assigneeId: request.AssigneeId,
            dueAtUtc: request.DueAtUtc,
            estimateMinutes: request.EstimateMinutes,
            status: initialStatus,
            issueType: request.IssueType,
            storyPoints: request.StoryPoints,
            epicId: request.EpicId,
            parentTaskId: request.ParentTaskId,
            sprintId: request.SprintId,
            boardRank: maxRank + 100
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

        if (task.SprintId.HasValue)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(task.SprintId.Value, cancellationToken);
        }

        var userMap = await GetUserMapAsync(new[] { callerId, request.AssigneeId ?? Guid.Empty }, cancellationToken);

        return await MapToSummaryWithContextAsync(task, project, userMap, cancellationToken);
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
        string? prState = null,
        Guid? sprintId = null,
        string? issueType = null,
        Guid? epicId = null,
        bool? backlogOnly = null)
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

        if (sprintId.HasValue)
        {
            query = query.Where(t => t.SprintId == sprintId.Value);
        }
        else if (backlogOnly == true)
        {
            query = query.Where(t => t.SprintId == null && t.IssueType != IssueTypes.Epic);
        }

        if (!string.IsNullOrWhiteSpace(issueType) && !string.Equals(issueType, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.IssueType == issueType);
        }

        if (epicId.HasValue)
        {
            query = query.Where(t => t.EpicId == epicId.Value);
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
            .OrderBy(t => t.BoardRank)
            .ThenByDescending(t => t.CreatedAtUtc)
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
                t.IssueType,
                t.StoryPoints,
                t.EpicId,
                EpicKey = t.Epic != null ? t.Epic.TaskKey : null,
                EpicTitle = t.Epic != null ? t.Epic.Title : null,
                t.ParentTaskId,
                ParentTaskKey = t.ParentTask != null ? t.ParentTask.TaskKey : null,
                t.SprintId,
                SprintName = t.Sprint != null ? t.Sprint.Name : null,
                t.BoardRank,
                SubtaskCount = t.Subtasks.Count,
                CompletedSubtaskCount = t.Subtasks.Count(st => st.Status == TaskStatuses.Done),
                IsBlockedByOpenIssue = t.IncomingLinks.Any(l =>
                    l.LinkType == IssueLinkTypes.Blocks &&
                    l.SourceTask != null &&
                    l.SourceTask.Status != TaskStatuses.Done &&
                    l.SourceTask.Status != TaskStatuses.Cancelled),
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
            GitSummary: gitSummaryMap.TryGetValue(t.Id, out var gs) ? gs : null,
            IssueType: t.IssueType,
            StoryPoints: t.StoryPoints,
            EpicId: t.EpicId,
            EpicKey: t.EpicKey,
            EpicTitle: t.EpicTitle,
            ParentTaskId: t.ParentTaskId,
            ParentTaskKey: t.ParentTaskKey,
            SprintId: t.SprintId,
            SprintName: t.SprintName,
            BoardRank: t.BoardRank,
            SubtaskCount: t.SubtaskCount,
            CompletedSubtaskCount: t.CompletedSubtaskCount,
            IsBlockedByOpenIssue: t.IsBlockedByOpenIssue
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
            .Include(t => t.Epic)
            .Include(t => t.ParentTask)
            .Include(t => t.Sprint)
            .Include(t => t.Subtasks)
            .Include(t => t.OutgoingLinks)
                .ThenInclude(l => l.TargetTask)
            .Include(t => t.IncomingLinks)
                .ThenInclude(l => l.SourceTask)
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
        userIds.AddRange(task.Subtasks.Where(st => st.AssigneeId.HasValue).Select(st => st.AssigneeId!.Value));

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

        var subtasks = task.Subtasks
            .OrderBy(st => st.TaskNumber)
            .Select(st => MapToSummary(st, task.Project, userMap))
            .ToList();

        var links = task.OutgoingLinks
            .Where(l => l.TargetTask != null)
            .Select(l => new IssueLinkDto(
                Id: l.Id,
                LinkType: l.LinkType,
                DisplayRelation: l.LinkType.ToLowerInvariant(),
                RelatedTaskId: l.TargetTask!.Id,
                RelatedTaskKey: l.TargetTask.TaskKey,
                RelatedTaskTitle: l.TargetTask.Title,
                RelatedTaskStatus: l.TargetTask.Status,
                RelatedTaskIssueType: l.TargetTask.IssueType,
                CreatedAtUtc: l.CreatedAtUtc))
            .Concat(task.IncomingLinks
                .Where(l => l.SourceTask != null)
                .Select(l => new IssueLinkDto(
                    Id: l.Id,
                    LinkType: l.LinkType,
                    DisplayRelation: IssueLinkTypes.GetInverseLabel(l.LinkType),
                    RelatedTaskId: l.SourceTask!.Id,
                    RelatedTaskKey: l.SourceTask.TaskKey,
                    RelatedTaskTitle: l.SourceTask.Title,
                    RelatedTaskStatus: l.SourceTask.Status,
                    RelatedTaskIssueType: l.SourceTask.IssueType,
                    CreatedAtUtc: l.CreatedAtUtc)))
            .OrderBy(l => l.CreatedAtUtc)
            .ToList();

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
            PullRequests: pullRequests,
            IssueType: task.IssueType,
            StoryPoints: task.StoryPoints,
            EpicId: task.EpicId,
            EpicKey: task.Epic?.TaskKey,
            EpicTitle: task.Epic?.Title,
            ParentTaskId: task.ParentTaskId,
            ParentTaskKey: task.ParentTask?.TaskKey,
            SprintId: task.SprintId,
            SprintName: task.Sprint?.Name,
            BoardRank: task.BoardRank,
            Subtasks: subtasks,
            Links: links
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

        var effectiveIssueType = string.IsNullOrWhiteSpace(request.IssueType) ? task.IssueType : request.IssueType;
        var previousSprintId = task.SprintId;

        await ValidateAgileReferencesAsync(
            task.ProjectId,
            effectiveIssueType,
            request.EpicId,
            request.ParentTaskId,
            request.SprintId,
            selfId: task.Id,
            cancellationToken);

        task.UpdateDetails(
            title: request.Title,
            description: request.Description,
            priority: request.Priority,
            assigneeId: request.AssigneeId,
            dueAtUtc: request.DueAtUtc,
            estimateMinutes: request.EstimateMinutes,
            expectedVersion: request.Version
        );

        task.SetAgileProperties(
            effectiveIssueType,
            request.StoryPoints,
            request.EpicId,
            request.ParentTaskId,
            request.SprintId);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Task has been modified by another operation. Please refresh.", ex);
        }

        if (previousSprintId.HasValue)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(previousSprintId.Value, cancellationToken);
        }
        if (task.SprintId.HasValue && task.SprintId != previousSprintId)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(task.SprintId.Value, cancellationToken);
        }

        var userMap = await GetUserMapAsync(new[] { task.CreatedById, task.AssigneeId ?? Guid.Empty }, cancellationToken);
        return await MapToSummaryWithContextAsync(task, task.Project, userMap, cancellationToken);
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

        await EnsureWipLimitAllowsEntryAsync(task.ProjectId, request.Status, task.Id, cancellationToken);

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

        if (task.SprintId.HasValue)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(task.SprintId.Value, cancellationToken);
        }

        var userMap = await GetUserMapAsync(new[] { task.CreatedById, task.AssigneeId ?? Guid.Empty }, cancellationToken);
        return await MapToSummaryWithContextAsync(task, task.Project, userMap, cancellationToken);
    }

    public async Task<TaskSummaryDto> MoveOnBoardAsync(
        Guid taskId,
        MoveTaskOnBoardRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _moveValidator.ValidateAsync(request, cancellationToken);
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

        if (!string.Equals(task.Status, request.TargetStatus, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureWipLimitAllowsEntryAsync(task.ProjectId, request.TargetStatus, task.Id, cancellationToken);
        }

        var history = task.MoveOnBoard(
            targetStatus: request.TargetStatus,
            boardRank: request.BoardRank,
            changedById: callerId,
            expectedVersion: request.Version,
            comment: request.Comment);

        if (history != null)
        {
            _context.TaskStatusHistories.Add(history);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Task has been modified by another operation. Please refresh.", ex);
        }

        if (history != null && task.SprintId.HasValue)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(task.SprintId.Value, cancellationToken);
        }

        var userMap = await GetUserMapAsync(new[] { task.CreatedById, task.AssigneeId ?? Guid.Empty }, cancellationToken);
        return await MapToSummaryWithContextAsync(task, task.Project, userMap, cancellationToken);
    }

    public async Task<TaskSummaryDto> AssignSprintAsync(
        Guid taskId,
        AssignTaskSprintRequest request,
        CancellationToken cancellationToken)
    {
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

        if (string.Equals(task.IssueType, IssueTypes.Epic, StringComparison.OrdinalIgnoreCase) && request.SprintId.HasValue)
        {
            throw new DomainException("Epics span across sprints and cannot be assigned directly to a sprint.");
        }

        if (request.SprintId.HasValue)
        {
            var sprint = await _context.Sprints
                .FirstOrDefaultAsync(s => s.Id == request.SprintId.Value && s.ProjectId == task.ProjectId, cancellationToken);
            if (sprint == null)
            {
                throw new KeyNotFoundException($"Sprint {request.SprintId.Value} not found in this project.");
            }
            if (sprint.Status == SprintStatuses.Closed)
            {
                throw new DomainException("Cannot assign issues to a closed sprint.");
            }
        }

        var previousSprintId = task.SprintId;
        task.AssignToSprint(request.SprintId, request.BoardRank);
        await _context.SaveChangesAsync(cancellationToken);

        if (previousSprintId.HasValue)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(previousSprintId.Value, cancellationToken);
        }
        if (task.SprintId.HasValue && task.SprintId != previousSprintId)
        {
            await _sprintService.RecordActiveSprintSnapshotAsync(task.SprintId.Value, cancellationToken);
        }

        var userMap = await GetUserMapAsync(new[] { task.CreatedById, task.AssigneeId ?? Guid.Empty }, cancellationToken);
        return await MapToSummaryWithContextAsync(task, task.Project, userMap, cancellationToken);
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

    private async Task ValidateAgileReferencesAsync(
        Guid projectId,
        string issueType,
        Guid? epicId,
        Guid? parentTaskId,
        Guid? sprintId,
        Guid selfId,
        CancellationToken cancellationToken)
    {
        if (epicId.HasValue)
        {
            var epic = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == epicId.Value && t.ProjectId == projectId, cancellationToken);
            if (epic == null || !string.Equals(epic.IssueType, IssueTypes.Epic, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("EpicId must reference an Epic in the same project.");
            }
        }

        if (string.Equals(issueType, IssueTypes.Subtask, StringComparison.OrdinalIgnoreCase))
        {
            if (!parentTaskId.HasValue || parentTaskId.Value == Guid.Empty)
            {
                throw new DomainException("A Subtask must specify a ParentTaskId.");
            }
            var parent = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == parentTaskId.Value && t.ProjectId == projectId, cancellationToken);
            if (parent == null)
            {
                throw new DomainException("ParentTaskId must reference a task in the same project.");
            }
            if (string.Equals(parent.IssueType, IssueTypes.Subtask, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parent.IssueType, IssueTypes.Epic, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("Subtasks can only be nested under a Story, Task, or Bug.");
            }
            if (selfId != Guid.Empty && parent.Id == selfId)
            {
                throw new DomainException("An issue cannot be its own parent.");
            }
        }

        if (sprintId.HasValue)
        {
            if (string.Equals(issueType, IssueTypes.Epic, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("Epics cannot be assigned directly to a sprint.");
            }
            var sprint = await _context.Sprints
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sprintId.Value && s.ProjectId == projectId, cancellationToken);
            if (sprint == null)
            {
                throw new DomainException("SprintId must reference a sprint in the same project.");
            }
            if (sprint.Status == SprintStatuses.Closed)
            {
                throw new DomainException("Cannot assign an issue to a closed sprint.");
            }
        }
    }

    private async Task EnsureWipLimitAllowsEntryAsync(
        Guid projectId,
        string targetStatus,
        Guid movingTaskId,
        CancellationToken cancellationToken)
    {
        var config = await _context.BoardColumnConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Status == targetStatus, cancellationToken);

        if (config == null || !config.IsStrictWip || !config.WipLimit.HasValue)
        {
            return;
        }

        var currentCount = await _context.Tasks
            .AsNoTracking()
            .CountAsync(t => t.ProjectId == projectId && t.Status == targetStatus && t.Id != movingTaskId, cancellationToken);

        if (currentCount >= config.WipLimit.Value)
        {
            throw new DomainException($"WIP limit ({config.WipLimit.Value}) reached for column '{targetStatus}'.");
        }
    }

    private async Task<TaskSummaryDto> MapToSummaryWithContextAsync(
        TaskItem task,
        Project project,
        IDictionary<Guid, (string DisplayName, string Email)> userMap,
        CancellationToken cancellationToken)
    {
        var gitSummaryMap = await BuildGitSummaryMapAsync(new[] { task.Id }, cancellationToken);

        string? epicKey = null;
        string? epicTitle = null;
        if (task.EpicId.HasValue)
        {
            var epic = await _context.Tasks.AsNoTracking()
                .Where(t => t.Id == task.EpicId.Value)
                .Select(t => new { t.TaskKey, t.Title })
                .FirstOrDefaultAsync(cancellationToken);
            epicKey = epic?.TaskKey;
            epicTitle = epic?.Title;
        }

        string? parentKey = null;
        if (task.ParentTaskId.HasValue)
        {
            parentKey = await _context.Tasks.AsNoTracking()
                .Where(t => t.Id == task.ParentTaskId.Value)
                .Select(t => t.TaskKey)
                .FirstOrDefaultAsync(cancellationToken);
        }

        string? sprintName = null;
        if (task.SprintId.HasValue)
        {
            sprintName = await _context.Sprints.AsNoTracking()
                .Where(s => s.Id == task.SprintId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var subtaskStats = await _context.Tasks.AsNoTracking()
            .Where(st => st.ParentTaskId == task.Id)
            .Select(st => st.Status)
            .ToListAsync(cancellationToken);

        var isBlocked = await _context.IssueLinks.AsNoTracking()
            .AnyAsync(l => l.TargetTaskId == task.Id &&
                           l.LinkType == IssueLinkTypes.Blocks &&
                           l.SourceTask != null &&
                           l.SourceTask.Status != TaskStatuses.Done &&
                           l.SourceTask.Status != TaskStatuses.Cancelled,
                cancellationToken);

        return MapToSummary(
            task,
            project,
            userMap,
            gitSummaryMap.GetValueOrDefault(task.Id),
            epicKey,
            epicTitle,
            parentKey,
            sprintName,
            subtaskStats.Count,
            subtaskStats.Count(s => s == TaskStatuses.Done),
            isBlocked);
    }

    private static TaskSummaryDto MapToSummary(
        TaskItem task,
        Project project,
        IDictionary<Guid, (string DisplayName, string Email)> userMap,
        TaskGitSummaryDto? gitSummary = null,
        string? epicKey = null,
        string? epicTitle = null,
        string? parentTaskKey = null,
        string? sprintName = null,
        int subtaskCount = 0,
        int completedSubtaskCount = 0,
        bool isBlockedByOpenIssue = false)
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
            GitSummary: gitSummary,
            IssueType: task.IssueType,
            StoryPoints: task.StoryPoints,
            EpicId: task.EpicId,
            EpicKey: epicKey,
            EpicTitle: epicTitle,
            ParentTaskId: task.ParentTaskId,
            ParentTaskKey: parentTaskKey,
            SprintId: task.SprintId,
            SprintName: sprintName,
            BoardRank: task.BoardRank,
            SubtaskCount: subtaskCount,
            CompletedSubtaskCount: completedSubtaskCount,
            IsBlockedByOpenIssue: isBlockedByOpenIssue
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
