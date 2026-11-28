using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Features.Git.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Git.Services;

public class GitIntegrationAppService : IGitIntegrationAppService
{
    public const string AutomationUserEmail = "git-automation@workflowpro.local";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IGitSecretProtector _secretProtector;
    private readonly IAuditService _auditService;
    private readonly IEnumerable<IGitProviderClient> _providerClients;
    private readonly IValidator<ConnectGitRepositoryRequest> _connectValidator;
    private readonly IValidator<UpdateGitRepositoryRequest> _updateValidator;
    private readonly IValidator<CreateTaskBranchRequest> _createBranchValidator;
    private readonly IValidator<LinkPullRequestRequest> _linkPrValidator;

    public GitIntegrationAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IGitSecretProtector secretProtector,
        IAuditService auditService,
        IEnumerable<IGitProviderClient> providerClients,
        IValidator<ConnectGitRepositoryRequest> connectValidator,
        IValidator<UpdateGitRepositoryRequest> updateValidator,
        IValidator<CreateTaskBranchRequest> createBranchValidator,
        IValidator<LinkPullRequestRequest> linkPrValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _secretProtector = secretProtector;
        _auditService = auditService;
        _providerClients = providerClients;
        _connectValidator = connectValidator;
        _updateValidator = updateValidator;
        _createBranchValidator = createBranchValidator;
        _linkPrValidator = linkPrValidator;
    }

    public async Task<IReadOnlyList<ProjectGitRepositoryDto>> GetProjectRepositoriesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        var repos = await _context.ProjectGitRepositories
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.IsDefault)
            .ThenBy(r => r.RepositoryFullName)
            .ToListAsync(cancellationToken);

        return repos.Select(MapRepositoryDto).ToList();
    }

    public async Task<ConnectGitRepositoryResponse> ConnectRepositoryAsync(
        Guid projectId,
        ConnectGitRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _connectValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageProjectGit(project, callerId);

        if (project.IsArchived)
        {
            throw new DomainException("Cannot connect Git repositories to an archived project.");
        }

        var fullName = $"{request.RepositoryOwner.Trim()}/{request.RepositoryName.Trim()}";
        var duplicate = await _context.ProjectGitRepositories
            .AnyAsync(r => r.ProjectId == projectId &&
                           r.Provider == request.Provider &&
                           r.RepositoryFullName.ToLower() == fullName.ToLower(), cancellationToken);
        if (duplicate)
        {
            throw new DomainException($"Repository '{fullName}' is already connected to this project.");
        }

        if (request.IsDefault)
        {
            var existingDefaults = await _context.ProjectGitRepositories
                .Where(r => r.ProjectId == projectId && r.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingDefaults)
            {
                existing.SetDefault(false);
            }
        }

        var repo = new ProjectGitRepository(
            projectId: projectId,
            provider: request.Provider,
            repositoryOwner: request.RepositoryOwner,
            repositoryName: request.RepositoryName,
            defaultBranch: string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch,
            createdById: callerId,
            apiBaseUrl: request.ApiBaseUrl,
            webBaseUrl: request.WebBaseUrl,
            isDefault: request.IsDefault,
            autoLinkEnabled: request.AutoLinkEnabled,
            autoTransitionEnabled: request.AutoTransitionEnabled,
            useInReviewColumn: request.UseInReviewColumn
        );

        string? generatedSecret = null;
        var rawSecret = string.IsNullOrWhiteSpace(request.WebhookSecret)
            ? (generatedSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant())
            : request.WebhookSecret.Trim();

        var encryptedToken = !string.IsNullOrWhiteSpace(request.AccessToken)
            ? _secretProtector.Protect(request.AccessToken.Trim())
            : null;
        var encryptedSecret = _secretProtector.Protect(rawSecret);

        repo.SetCredentials(encryptedToken, encryptedSecret);

        _context.ProjectGitRepositories.Add(repo);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.GitRepositoryConnected,
            "ProjectGitRepository",
            repo.Id,
            new { repo.ProjectId, repo.Provider, repo.RepositoryFullName },
            cancellationToken);

        return new ConnectGitRepositoryResponse(MapRepositoryDto(repo), generatedSecret);
    }

    public async Task<ProjectGitRepositoryDto> UpdateRepositoryAsync(
        Guid projectId,
        Guid repositoryId,
        UpdateGitRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageProjectGit(project, callerId);

        var repo = await _context.ProjectGitRepositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.ProjectId == projectId, cancellationToken);
        if (repo == null)
        {
            throw new KeyNotFoundException($"Git repository {repositoryId} not found.");
        }

        if (request.IsDefault && !repo.IsDefault)
        {
            var existingDefaults = await _context.ProjectGitRepositories
                .Where(r => r.ProjectId == projectId && r.Id != repositoryId && r.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingDefaults)
            {
                existing.SetDefault(false);
            }
        }

        repo.UpdateConfiguration(
            defaultBranch: request.DefaultBranch,
            apiBaseUrl: request.ApiBaseUrl,
            webBaseUrl: request.WebBaseUrl,
            isDefault: request.IsDefault,
            isActive: request.IsActive,
            autoLinkEnabled: request.AutoLinkEnabled,
            autoTransitionEnabled: request.AutoTransitionEnabled,
            useInReviewColumn: request.UseInReviewColumn
        );

        var newEncryptedToken = !string.IsNullOrWhiteSpace(request.AccessToken)
            ? _secretProtector.Protect(request.AccessToken.Trim())
            : null;
        var newEncryptedSecret = !string.IsNullOrWhiteSpace(request.WebhookSecret)
            ? _secretProtector.Protect(request.WebhookSecret.Trim())
            : null;

        if (newEncryptedToken != null || newEncryptedSecret != null)
        {
            repo.SetCredentials(newEncryptedToken, newEncryptedSecret);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.GitRepositoryUpdated,
            "ProjectGitRepository",
            repo.Id,
            new { repo.ProjectId, repo.RepositoryFullName, repo.IsActive, repo.AutoTransitionEnabled, repo.UseInReviewColumn },
            cancellationToken);

        return MapRepositoryDto(repo);
    }

    public async Task DisconnectRepositoryAsync(
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageProjectGit(project, callerId);

        var repo = await _context.ProjectGitRepositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.ProjectId == projectId, cancellationToken);
        if (repo == null)
        {
            throw new KeyNotFoundException($"Git repository {repositoryId} not found.");
        }

        _context.ProjectGitRepositories.Remove(repo);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.GitRepositoryDisconnected,
            "ProjectGitRepository",
            repositoryId,
            new { projectId, repo.RepositoryFullName },
            cancellationToken);
    }

    public async Task<GitConnectionTestResultDto> TestRepositoryConnectionAsync(
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageProjectGit(project, callerId);

        var repo = await _context.ProjectGitRepositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.ProjectId == projectId, cancellationToken);
        if (repo == null)
        {
            throw new KeyNotFoundException($"Git repository {repositoryId} not found.");
        }

        var client = ResolveProviderClient(repo.Provider);
        var context = BuildProviderContext(repo);
        var result = await client.TestConnectionAsync(context, cancellationToken);

        if (result.Success)
        {
            repo.RecordSyncSuccess(DateTime.UtcNow);
        }
        else
        {
            repo.RecordSyncFailure(result.Message, DateTime.UtcNow);
        }
        await _context.SaveChangesAsync(cancellationToken);

        return new GitConnectionTestResultDto(result.Success, result.Message, result.DefaultBranch, result.IsPrivate);
    }

    public async Task<GitSyncResultDto> SyncRepositoryPullRequestsAsync(
        Guid projectId,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var project = await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);
        EnsureCanManageProjectGit(project, callerId);

        var repo = await _context.ProjectGitRepositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.ProjectId == projectId, cancellationToken);
        if (repo == null)
        {
            throw new KeyNotFoundException($"Git repository {repositoryId} not found.");
        }

        if (!repo.IsActive)
        {
            throw new DomainException("Cannot sync an inactive Git repository connection.");
        }

        var client = ResolveProviderClient(repo.Provider);
        var context = BuildProviderContext(repo);

        IReadOnlyList<GitRemotePullRequest> remotePrs;
        try
        {
            remotePrs = await client.ListPullRequestsAsync(context, "all", 30, cancellationToken);
        }
        catch (Exception ex)
        {
            repo.RecordSyncFailure(ex.Message, DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            throw new DomainException($"Failed to sync pull requests from {repo.Provider}: {ex.Message}");
        }

        var linkedCount = 0;
        var transitionedCount = 0;

        foreach (var remotePr in remotePrs)
        {
            var (_, linked, transitioned) = await UpsertPullRequestAndLinkAsync(
                repo,
                remotePr,
                callerId,
                notifyOnNewOrMerged: false,
                cancellationToken);
            linkedCount += linked;
            transitionedCount += transitioned;
        }

        var now = DateTime.UtcNow;
        repo.RecordSyncSuccess(now);
        await _context.SaveChangesAsync(cancellationToken);

        return new GitSyncResultDto(repo.Id, remotePrs.Count, linkedCount, transitionedCount, now);
    }

    public async Task<IReadOnlyList<ProjectPullRequestSummaryDto>> GetProjectPullRequestsAsync(
        Guid projectId,
        string? state,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await GetAuthorizedProjectAsync(projectId, callerId, cancellationToken);

        var query = _context.GitPullRequests
            .AsNoTracking()
            .Include(pr => pr.Repository)
            .Include(pr => pr.TaskLinks)
                .ThenInclude(l => l.Task)
            .Where(pr => pr.Repository != null && pr.Repository.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(pr => pr.State == state);
        }

        var prs = await query
            .OrderByDescending(pr => pr.OpenedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return prs.Select(pr => new ProjectPullRequestSummaryDto(
            Id: pr.Id,
            RepositoryId: pr.RepositoryId,
            RepositoryFullName: pr.Repository?.RepositoryFullName ?? string.Empty,
            PrNumber: pr.PrNumber,
            Title: pr.Title,
            State: pr.State,
            SourceBranch: pr.SourceBranch,
            TargetBranch: pr.TargetBranch,
            AuthorLogin: pr.AuthorLogin,
            HtmlUrl: pr.HtmlUrl,
            ReviewStatus: pr.ReviewStatus,
            CheckStatus: pr.CheckStatus,
            LinkedTaskKeys: pr.TaskLinks
                .Where(l => l.Task != null && !string.IsNullOrEmpty(l.Task.TaskKey))
                .Select(l => l.Task!.TaskKey)
                .Distinct()
                .ToList(),
            OpenedAtUtc: pr.OpenedAtUtc,
            MergedAtUtc: pr.MergedAtUtc,
            LastSyncedAtUtc: pr.LastSyncedAtUtc
        )).ToList();
    }

    public async Task<TaskGitDetailDto> GetTaskGitDetailsAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var task = await GetAuthorizedTaskAsync(taskId, callerId, cancellationToken);

        var branches = await _context.TaskGitBranches
            .AsNoTracking()
            .Include(b => b.Repository)
            .Where(b => b.TaskId == taskId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var prLinks = await _context.TaskPullRequestLinks
            .AsNoTracking()
            .Include(l => l.PullRequest)
                .ThenInclude(pr => pr!.Repository)
            .Where(l => l.TaskId == taskId && l.PullRequest != null)
            .OrderByDescending(l => l.PullRequest!.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return new TaskGitDetailDto(
            TaskId: task.Id,
            TaskKey: task.TaskKey,
            Branches: branches.Select(MapBranchDto).ToList(),
            PullRequests: prLinks.Select(MapPullRequestDto).ToList()
        );
    }

    public async Task<TaskGitBranchDto> CreateTaskBranchAsync(
        Guid taskId,
        CreateTaskBranchRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _createBranchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var task = await GetAuthorizedTaskAsync(taskId, callerId, cancellationToken);

        if (task.Project!.IsArchived)
        {
            throw new DomainException("Cannot create Git branches for tasks in an archived project.");
        }

        ProjectGitRepository? repo;
        if (request.RepositoryId.HasValue)
        {
            repo = await _context.ProjectGitRepositories
                .FirstOrDefaultAsync(r => r.Id == request.RepositoryId.Value && r.ProjectId == task.ProjectId, cancellationToken);
        }
        else
        {
            repo = await _context.ProjectGitRepositories
                .Where(r => r.ProjectId == task.ProjectId && r.IsActive)
                .OrderByDescending(r => r.IsDefault)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (repo == null)
        {
            throw new DomainException("No active Git repository is connected to this project. Connect a repository in Project Settings first.");
        }
        if (!repo.IsActive)
        {
            throw new DomainException("Selected Git repository connection is inactive.");
        }
        if (!repo.HasAccessToken)
        {
            throw new DomainException("A Git access token is required on the project repository to create branches remotely.");
        }

        var effectiveKey = string.IsNullOrWhiteSpace(task.TaskKey)
            ? $"{task.Project.ProjectCode}-{task.Id.ToString("N")[..6].ToUpperInvariant()}"
            : task.TaskKey;

        var branchName = string.IsNullOrWhiteSpace(request.BranchName)
            ? TaskKeyExtractor.BuildDefaultBranchName(effectiveKey, task.Title, request.BranchPrefix ?? "feat")
            : request.BranchName.Trim();

        var baseBranch = string.IsNullOrWhiteSpace(request.BaseBranch)
            ? repo.DefaultBranch
            : request.BaseBranch.Trim();

        var existsLocally = await _context.TaskGitBranches
            .AnyAsync(b => b.RepositoryId == repo.Id && b.BranchName.ToLower() == branchName.ToLower(), cancellationToken);
        if (existsLocally)
        {
            throw new DomainException($"Branch '{branchName}' is already tracked in this repository.");
        }

        var client = ResolveProviderClient(repo.Provider);
        var providerCtx = BuildProviderContext(repo);
        var remoteResult = await client.CreateBranchAsync(providerCtx, branchName, baseBranch, cancellationToken);

        var branch = new TaskGitBranch(
            taskId: task.Id,
            repositoryId: repo.Id,
            branchName: remoteResult.BranchName,
            baseBranch: remoteResult.BaseBranch,
            branchUrl: remoteResult.BranchUrl,
            createdByUserId: callerId
        );

        _context.TaskGitBranches.Add(branch);

        // Auto-transition Backlog / ToDo -> InProgress on branch creation
        if (repo.AutoTransitionEnabled &&
            (string.Equals(task.Status, TaskStatuses.Backlog, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(task.Status, TaskStatuses.ToDo, StringComparison.OrdinalIgnoreCase)))
        {
            var histories = task.ApplyAutomationTransition(
                TaskStatuses.InProgress,
                callerId,
                $"Auto-transitioned on branch '{branch.BranchName}' creation.");
            foreach (var h in histories)
            {
                _context.TaskStatusHistories.Add(h);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.GitBranchCreated,
            "TaskGitBranch",
            branch.Id,
            new { task.Id, task.TaskKey, repo.RepositoryFullName, branch.BranchName, branch.BaseBranch },
            cancellationToken);

        return new TaskGitBranchDto(
            Id: branch.Id,
            TaskId: branch.TaskId,
            RepositoryId: branch.RepositoryId,
            RepositoryFullName: repo.RepositoryFullName,
            BranchName: branch.BranchName,
            BaseBranch: branch.BaseBranch,
            BranchUrl: branch.BranchUrl,
            CreatedByUserId: branch.CreatedByUserId,
            CreatedAtUtc: branch.CreatedAtUtc
        );
    }

    public async Task<TaskPullRequestDto> LinkPullRequestToTaskAsync(
        Guid taskId,
        LinkPullRequestRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _linkPrValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var callerId = RequireCallerId();
        var task = await GetAuthorizedTaskAsync(taskId, callerId, cancellationToken);

        if (task.Project!.IsArchived)
        {
            throw new DomainException("Cannot link pull requests in an archived project.");
        }

        var repo = await _context.ProjectGitRepositories
            .FirstOrDefaultAsync(r => r.Id == request.RepositoryId && r.ProjectId == task.ProjectId, cancellationToken);
        if (repo == null)
        {
            throw new KeyNotFoundException($"Git repository {request.RepositoryId} not found in this project.");
        }

        var pr = await _context.GitPullRequests
            .FirstOrDefaultAsync(p => p.RepositoryId == repo.Id && p.PrNumber == request.PrNumber, cancellationToken);

        if (pr == null)
        {
            if (!repo.HasAccessToken)
            {
                throw new DomainException($"Pull request #{request.PrNumber} is not synced yet and no access token is configured to fetch it.");
            }

            var client = ResolveProviderClient(repo.Provider);
            var remotePr = await client.GetPullRequestAsync(BuildProviderContext(repo), request.PrNumber, cancellationToken);
            if (remotePr == null)
            {
                throw new KeyNotFoundException($"Pull request #{request.PrNumber} was not found in {repo.RepositoryFullName}.");
            }

            pr = new GitPullRequest(
                repositoryId: repo.Id,
                externalPrId: remotePr.ExternalPrId,
                prNumber: remotePr.PrNumber,
                title: remotePr.Title,
                body: remotePr.Body,
                state: remotePr.State,
                sourceBranch: remotePr.SourceBranch,
                targetBranch: remotePr.TargetBranch,
                authorLogin: remotePr.AuthorLogin,
                htmlUrl: remotePr.HtmlUrl,
                openedAtUtc: remotePr.OpenedAtUtc,
                authorAvatarUrl: remotePr.AuthorAvatarUrl,
                reviewStatus: remotePr.ReviewStatus,
                checkStatus: remotePr.CheckStatus,
                commitsCount: remotePr.CommitsCount,
                additions: remotePr.Additions,
                deletions: remotePr.Deletions,
                mergedAtUtc: remotePr.MergedAtUtc,
                closedAtUtc: remotePr.ClosedAtUtc
            );
            _context.GitPullRequests.Add(pr);
        }

        var existingLink = await _context.TaskPullRequestLinks
            .FirstOrDefaultAsync(l => l.TaskId == task.Id && l.PullRequestId == pr.Id, cancellationToken);
        if (existingLink != null)
        {
            throw new DomainException($"Pull request #{pr.PrNumber} is already linked to this task.");
        }

        var link = new TaskPullRequestLink(task.Id, pr.Id, TaskPullRequestLinkSources.Manual, callerId);
        _context.TaskPullRequestLinks.Add(link);

        if (repo.AutoTransitionEnabled)
        {
            ApplyPrTransitionToTask(task, pr, repo, callerId);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.GitPullRequestLinked,
            "TaskPullRequestLink",
            link.Id,
            new { task.Id, task.TaskKey, repo.RepositoryFullName, pr.PrNumber },
            cancellationToken);

        return new TaskPullRequestDto(
            Id: pr.Id,
            RepositoryId: pr.RepositoryId,
            RepositoryFullName: repo.RepositoryFullName,
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
            LinkSource: link.LinkSource,
            OpenedAtUtc: pr.OpenedAtUtc,
            MergedAtUtc: pr.MergedAtUtc,
            ClosedAtUtc: pr.ClosedAtUtc,
            LastSyncedAtUtc: pr.LastSyncedAtUtc
        );
    }

    public async Task UnlinkPullRequestFromTaskAsync(
        Guid taskId,
        Guid pullRequestId,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var task = await GetAuthorizedTaskAsync(taskId, callerId, cancellationToken);

        var link = await _context.TaskPullRequestLinks
            .FirstOrDefaultAsync(l => l.TaskId == task.Id && l.PullRequestId == pullRequestId, cancellationToken);
        if (link == null)
        {
            throw new KeyNotFoundException("Pull request link not found for this task.");
        }

        _context.TaskPullRequestLinks.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.GitPullRequestUnlinked,
            "TaskPullRequestLink",
            link.Id,
            new { task.Id, pullRequestId },
            cancellationToken);
    }

    public async Task<GitWebhookProcessResultDto> ProcessWebhookAsync(
        string provider,
        Guid repositoryId,
        string? deliveryId,
        string? eventType,
        string? signatureHeader,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        if (!GitProviders.IsSupported(provider))
        {
            throw new KeyNotFoundException($"Unsupported Git provider '{provider}'.");
        }

        var repo = await _context.ProjectGitRepositories
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

        if (repo == null || !repo.IsActive || !string.Equals(repo.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException($"Git repository {repositoryId} not found or inactive.");
        }

        if (string.IsNullOrWhiteSpace(repo.EncryptedWebhookSecret))
        {
            throw new UnauthorizedAccessException("Webhook secret is not configured for this repository.");
        }

        var client = ResolveProviderClient(repo.Provider);
        var secret = _secretProtector.Unprotect(repo.EncryptedWebhookSecret);

        if (!client.VerifyWebhookSignature(rawPayload, signatureHeader, secret))
        {
            throw new UnauthorizedAccessException("Invalid webhook signature.");
        }

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
        var effectiveDeliveryId = string.IsNullOrWhiteSpace(deliveryId) ? payloadHash : deliveryId.Trim();
        var effectiveEventType = string.IsNullOrWhiteSpace(eventType) ? "unknown" : eventType.Trim();

        var alreadyProcessed = await _context.GitWebhookDeliveries
            .AnyAsync(d => d.Provider == repo.Provider && d.DeliveryId == effectiveDeliveryId, cancellationToken);
        if (alreadyProcessed)
        {
            return new GitWebhookProcessResultDto(true, GitWebhookDeliveryStatuses.Ignored, "Duplicate webhook delivery ignored.");
        }

        var delivery = new GitWebhookDelivery(repo.Id, repo.Provider, effectiveDeliveryId, effectiveEventType, payloadHash);
        _context.GitWebhookDeliveries.Add(delivery);

        if (string.Equals(effectiveEventType, "ping", StringComparison.OrdinalIgnoreCase))
        {
            delivery.MarkProcessed(GitWebhookDeliveryStatuses.Processed);
            await _context.SaveChangesAsync(cancellationToken);
            return new GitWebhookProcessResultDto(true, GitWebhookDeliveryStatuses.Processed, "Webhook ping acknowledged.");
        }

        var parsed = client.ParseWebhookEvent(effectiveEventType, rawPayload);
        if (parsed == null)
        {
            delivery.MarkProcessed(GitWebhookDeliveryStatuses.Ignored, $"Event '{effectiveEventType}' is not handled.");
            await _context.SaveChangesAsync(cancellationToken);
            return new GitWebhookProcessResultDto(true, GitWebhookDeliveryStatuses.Ignored, $"Event '{effectiveEventType}' ignored.");
        }

        var actorId = await ResolveAutomationActorIdAsync(repo, cancellationToken);
        var tasksLinked = 0;
        var tasksTransitioned = 0;

        try
        {
            if (string.Equals(parsed.EventType, "create", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parsed.BranchName))
            {
                (tasksLinked, tasksTransitioned) = await HandleBranchCreatedWebhookAsync(repo, parsed, actorId, cancellationToken);
            }
            else if (string.Equals(parsed.EventType, "pull_request", StringComparison.OrdinalIgnoreCase) &&
                     parsed.PullRequest != null)
            {
                var (_, linked, transitioned) = await UpsertPullRequestAndLinkAsync(
                    repo,
                    parsed.PullRequest,
                    actorId,
                    notifyOnNewOrMerged: true,
                    cancellationToken);
                tasksLinked = linked;
                tasksTransitioned = transitioned;
            }
            else if (string.Equals(parsed.EventType, "pull_request_review", StringComparison.OrdinalIgnoreCase) &&
                     parsed.RelatedPrNumber.HasValue &&
                     !string.IsNullOrWhiteSpace(parsed.ReviewState))
            {
                await HandlePullRequestReviewWebhookAsync(repo, parsed.RelatedPrNumber.Value, parsed.ReviewState, parsed.SenderLogin, cancellationToken);
            }
            else if ((string.Equals(parsed.EventType, "check_suite", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(parsed.EventType, "check_run", StringComparison.OrdinalIgnoreCase)) &&
                     !string.IsNullOrWhiteSpace(parsed.HeadBranch) &&
                     !string.IsNullOrWhiteSpace(parsed.CheckConclusion))
            {
                await HandleCheckEventWebhookAsync(repo, parsed.HeadBranch, parsed.CheckConclusion, cancellationToken);
            }

            repo.RecordSyncSuccess(DateTime.UtcNow);
            delivery.MarkProcessed(GitWebhookDeliveryStatuses.Processed);
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.RecordAsync(
                AuditActions.GitWebhookProcessed,
                "ProjectGitRepository",
                repo.Id,
                new { repo.RepositoryFullName, EventType = effectiveEventType, DeliveryId = effectiveDeliveryId, tasksLinked, tasksTransitioned },
                cancellationToken);

            return new GitWebhookProcessResultDto(true, GitWebhookDeliveryStatuses.Processed, "Webhook processed.", tasksLinked, tasksTransitioned);
        }
        catch (Exception ex)
        {
            delivery.MarkProcessed(GitWebhookDeliveryStatuses.Failed, ex.Message);
            repo.RecordSyncFailure(ex.Message, DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<(int Linked, int Transitioned)> HandleBranchCreatedWebhookAsync(
        ProjectGitRepository repo,
        GitWebhookEvent parsed,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (!repo.AutoLinkEnabled || string.IsNullOrWhiteSpace(parsed.BranchName))
        {
            return (0, 0);
        }

        var keys = TaskKeyExtractor.ExtractTaskKeys(parsed.BranchName);
        if (keys.Count == 0) return (0, 0);

        var matchingTasks = await _context.Tasks
            .Include(t => t.StatusHistory)
            .Where(t => t.ProjectId == repo.ProjectId && keys.Contains(t.TaskKey))
            .ToListAsync(cancellationToken);

        var linked = 0;
        var transitioned = 0;

        foreach (var task in matchingTasks)
        {
            var exists = await _context.TaskGitBranches
                .AnyAsync(b => b.RepositoryId == repo.Id && b.BranchName.ToLower() == parsed.BranchName.ToLower(), cancellationToken);
            if (!exists)
            {
                var branchUrl = string.IsNullOrWhiteSpace(parsed.BranchUrl)
                    ? repo.BuildBranchWebUrl(parsed.BranchName)
                    : parsed.BranchUrl;

                _context.TaskGitBranches.Add(new TaskGitBranch(
                    taskId: task.Id,
                    repositoryId: repo.Id,
                    branchName: parsed.BranchName,
                    baseBranch: parsed.BaseBranch ?? repo.DefaultBranch,
                    branchUrl: branchUrl,
                    createdByUserId: null));
                linked++;
            }

            if (repo.AutoTransitionEnabled &&
                (string.Equals(task.Status, TaskStatuses.Backlog, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(task.Status, TaskStatuses.ToDo, StringComparison.OrdinalIgnoreCase)))
            {
                var histories = task.ApplyAutomationTransition(
                    TaskStatuses.InProgress,
                    actorId,
                    $"Auto-transitioned on Git branch '{parsed.BranchName}' creation.");
                foreach (var h in histories)
                {
                    _context.TaskStatusHistories.Add(h);
                }
                if (histories.Count > 0) transitioned++;
            }
        }

        return (linked, transitioned);
    }

    private async Task<(GitPullRequest Pr, int LinkedCount, int TransitionedCount)> UpsertPullRequestAndLinkAsync(
        ProjectGitRepository repo,
        GitRemotePullRequest remotePr,
        Guid actorId,
        bool notifyOnNewOrMerged,
        CancellationToken cancellationToken)
    {
        var existingPr = await _context.GitPullRequests
            .Include(p => p.TaskLinks)
                .ThenInclude(l => l.Task)
                    .ThenInclude(t => t!.StatusHistory)
            .FirstOrDefaultAsync(p => p.RepositoryId == repo.Id && p.PrNumber == remotePr.PrNumber, cancellationToken);

        var isNewPr = existingPr == null;
        var stateChanged = false;

        if (existingPr == null)
        {
            existingPr = new GitPullRequest(
                repositoryId: repo.Id,
                externalPrId: remotePr.ExternalPrId,
                prNumber: remotePr.PrNumber,
                title: remotePr.Title,
                body: remotePr.Body,
                state: remotePr.State,
                sourceBranch: remotePr.SourceBranch,
                targetBranch: remotePr.TargetBranch,
                authorLogin: remotePr.AuthorLogin,
                htmlUrl: remotePr.HtmlUrl,
                openedAtUtc: remotePr.OpenedAtUtc,
                authorAvatarUrl: remotePr.AuthorAvatarUrl,
                reviewStatus: remotePr.ReviewStatus,
                checkStatus: remotePr.CheckStatus,
                commitsCount: remotePr.CommitsCount,
                additions: remotePr.Additions,
                deletions: remotePr.Deletions,
                mergedAtUtc: remotePr.MergedAtUtc,
                closedAtUtc: remotePr.ClosedAtUtc
            );
            _context.GitPullRequests.Add(existingPr);
            stateChanged = true;
        }
        else
        {
            var changes = existingPr.UpdateFromRemote(
                externalPrId: remotePr.ExternalPrId,
                title: remotePr.Title,
                body: remotePr.Body,
                state: remotePr.State,
                sourceBranch: remotePr.SourceBranch,
                targetBranch: remotePr.TargetBranch,
                authorLogin: remotePr.AuthorLogin,
                htmlUrl: remotePr.HtmlUrl,
                authorAvatarUrl: remotePr.AuthorAvatarUrl,
                reviewStatus: remotePr.ReviewStatus,
                checkStatus: remotePr.CheckStatus,
                commitsCount: remotePr.CommitsCount,
                additions: remotePr.Additions,
                deletions: remotePr.Deletions,
                mergedAtUtc: remotePr.MergedAtUtc,
                closedAtUtc: remotePr.ClosedAtUtc);
            stateChanged = changes.StateChanged;
        }

        var linkedTasks = new Dictionary<Guid, TaskItem>();
        foreach (var existingLink in existingPr.TaskLinks)
        {
            if (existingLink.Task != null)
            {
                linkedTasks[existingLink.TaskId] = existingLink.Task;
            }
        }

        var newLinksAdded = 0;

        if (repo.AutoLinkEnabled)
        {
            var branchKeys = TaskKeyExtractor.ExtractTaskKeys(remotePr.SourceBranch);
            var titleKeys = TaskKeyExtractor.ExtractTaskKeys(remotePr.Title);
            var bodyKeys = TaskKeyExtractor.ExtractTaskKeys(remotePr.Body);
            var allKeys = branchKeys.Concat(titleKeys).Concat(bodyKeys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Also look up tasks already linked to the PR's source branch
            var branchMatchedTaskIds = await _context.TaskGitBranches
                .Where(b => b.RepositoryId == repo.Id && b.BranchName.ToLower() == remotePr.SourceBranch.ToLower())
                .Select(b => b.TaskId)
                .ToListAsync(cancellationToken);

            if (allKeys.Count > 0 || branchMatchedTaskIds.Count > 0)
            {
                var candidateTasks = await _context.Tasks
                    .Include(t => t.StatusHistory)
                    .Where(t => t.ProjectId == repo.ProjectId &&
                                (allKeys.Contains(t.TaskKey) || branchMatchedTaskIds.Contains(t.Id)))
                    .ToListAsync(cancellationToken);

                foreach (var task in candidateTasks)
                {
                    if (!linkedTasks.ContainsKey(task.Id))
                    {
                        var source = branchMatchedTaskIds.Contains(task.Id) || branchKeys.Contains(task.TaskKey, StringComparer.OrdinalIgnoreCase)
                            ? TaskPullRequestLinkSources.AutoBranch
                            : titleKeys.Contains(task.TaskKey, StringComparer.OrdinalIgnoreCase)
                                ? TaskPullRequestLinkSources.AutoTitle
                                : TaskPullRequestLinkSources.AutoBody;

                        _context.TaskPullRequestLinks.Add(new TaskPullRequestLink(task.Id, existingPr.Id, source));
                        linkedTasks[task.Id] = task;
                        newLinksAdded++;
                    }
                }
            }
        }

        var transitionedCount = 0;
        if (repo.AutoTransitionEnabled)
        {
            foreach (var task in linkedTasks.Values)
            {
                if (ApplyPrTransitionToTask(task, existingPr, repo, actorId))
                {
                    transitionedCount++;
                }
            }
        }

        if (notifyOnNewOrMerged && stateChanged)
        {
            DispatchPrNotifications(linkedTasks.Values, existingPr, isNewPr);
        }

        return (existingPr, newLinksAdded, transitionedCount);
    }

    private async Task HandlePullRequestReviewWebhookAsync(
        ProjectGitRepository repo,
        int prNumber,
        string reviewState,
        string? reviewerLogin,
        CancellationToken cancellationToken)
    {
        var pr = await _context.GitPullRequests
            .Include(p => p.TaskLinks)
                .ThenInclude(l => l.Task)
            .FirstOrDefaultAsync(p => p.RepositoryId == repo.Id && p.PrNumber == prNumber, cancellationToken);

        if (pr == null) return;

        pr.UpdateReviewStatus(reviewState);

        if (string.Equals(reviewState, PullRequestReviewStatuses.ChangesRequested, StringComparison.OrdinalIgnoreCase))
        {
            var reviewer = string.IsNullOrWhiteSpace(reviewerLogin) ? "A reviewer" : $"@{reviewerLogin}";
            foreach (var task in pr.TaskLinks.Where(l => l.Task != null).Select(l => l.Task!))
            {
                var recipientId = task.AssigneeId ?? task.CreatedById;
                _context.Notifications.Add(new InAppNotification(
                    recipientId,
                    NotificationTypes.PullRequestChangesRequested,
                    $"Changes Requested on PR #{pr.PrNumber}",
                    $"{reviewer} requested changes on PR #{pr.PrNumber} ({pr.Title}) for {task.TaskKey}.",
                    "Task",
                    task.Id));
            }
        }
    }

    private async Task HandleCheckEventWebhookAsync(
        ProjectGitRepository repo,
        string headBranch,
        string checkConclusion,
        CancellationToken cancellationToken)
    {
        var openPrs = await _context.GitPullRequests
            .Include(p => p.TaskLinks)
                .ThenInclude(l => l.Task)
            .Where(p => p.RepositoryId == repo.Id &&
                        p.SourceBranch.ToLower() == headBranch.ToLower() &&
                        (p.State == PullRequestStates.Open || p.State == PullRequestStates.Draft))
            .ToListAsync(cancellationToken);

        foreach (var pr in openPrs)
        {
            var previousStatus = pr.CheckStatus;
            pr.UpdateCheckStatus(checkConclusion);

            if (string.Equals(checkConclusion, PullRequestCheckStatuses.Failure, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(previousStatus, PullRequestCheckStatuses.Failure, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var task in pr.TaskLinks.Where(l => l.Task != null).Select(l => l.Task!))
                {
                    var recipientId = task.AssigneeId ?? task.CreatedById;
                    _context.Notifications.Add(new InAppNotification(
                        recipientId,
                        NotificationTypes.PullRequestChecksFailed,
                        $"CI Checks Failed on PR #{pr.PrNumber}",
                        $"CI checks failed on PR #{pr.PrNumber} ({pr.Title}) linked to {task.TaskKey}.",
                        "Task",
                        task.Id));
                }
            }
        }
    }

    private bool ApplyPrTransitionToTask(
        TaskItem task,
        GitPullRequest pr,
        ProjectGitRepository repo,
        Guid actorId)
    {
        string? targetStatus = null;
        string comment;

        if (string.Equals(pr.State, PullRequestStates.Merged, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(task.Status, TaskStatuses.Done, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(task.Status, TaskStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                targetStatus = TaskStatuses.Done;
            }
            comment = $"Auto-transitioned on PR #{pr.PrNumber} merge ({pr.Title}).";
        }
        else if (string.Equals(pr.State, PullRequestStates.Open, StringComparison.OrdinalIgnoreCase))
        {
            var desired = repo.UseInReviewColumn ? TaskStatuses.InReview : TaskStatuses.InProgress;
            if (string.Equals(task.Status, TaskStatuses.Backlog, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(task.Status, TaskStatuses.ToDo, StringComparison.OrdinalIgnoreCase) ||
                (repo.UseInReviewColumn && string.Equals(task.Status, TaskStatuses.InProgress, StringComparison.OrdinalIgnoreCase)))
            {
                targetStatus = desired;
            }
            comment = $"Auto-transitioned on PR #{pr.PrNumber} opened ({pr.Title}).";
        }
        else if (string.Equals(pr.State, PullRequestStates.Draft, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(task.Status, TaskStatuses.Backlog, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(task.Status, TaskStatuses.ToDo, StringComparison.OrdinalIgnoreCase))
            {
                targetStatus = TaskStatuses.InProgress;
            }
            comment = $"Auto-transitioned on Draft PR #{pr.PrNumber} ({pr.Title}).";
        }
        else if (string.Equals(pr.State, PullRequestStates.Closed, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(task.Status, TaskStatuses.InReview, StringComparison.OrdinalIgnoreCase))
            {
                targetStatus = TaskStatuses.InProgress;
            }
            comment = $"Auto-transitioned on PR #{pr.PrNumber} closed without merge.";
        }
        else
        {
            return false;
        }

        if (targetStatus == null) return false;

        var histories = task.ApplyAutomationTransition(targetStatus, actorId, comment);
        foreach (var h in histories)
        {
            _context.TaskStatusHistories.Add(h);
        }
        return histories.Count > 0;
    }

    private void DispatchPrNotifications(IEnumerable<TaskItem> tasks, GitPullRequest pr, bool isNewPr)
    {
        foreach (var task in tasks)
        {
            var recipientId = task.AssigneeId ?? task.CreatedById;
            if (string.Equals(pr.State, PullRequestStates.Merged, StringComparison.OrdinalIgnoreCase))
            {
                _context.Notifications.Add(new InAppNotification(
                    recipientId,
                    NotificationTypes.PullRequestMerged,
                    $"PR #{pr.PrNumber} Merged",
                    $"Pull request #{pr.PrNumber} ({pr.Title}) for {task.TaskKey} was merged by @{pr.AuthorLogin}.",
                    "Task",
                    task.Id));
            }
            else if (isNewPr || string.Equals(pr.State, PullRequestStates.Open, StringComparison.OrdinalIgnoreCase))
            {
                _context.Notifications.Add(new InAppNotification(
                    recipientId,
                    NotificationTypes.PullRequestOpened,
                    $"PR #{pr.PrNumber} Linked to {task.TaskKey}",
                    $"Pull request #{pr.PrNumber} ({pr.Title}) is now {pr.State} for {task.TaskKey}.",
                    "Task",
                    task.Id));
            }
        }
    }

    private async Task<Guid> ResolveAutomationActorIdAsync(ProjectGitRepository repo, CancellationToken cancellationToken)
    {
        var botId = await _identityService.FindUserIdByEmailAsync(AutomationUserEmail, cancellationToken);
        return botId ?? repo.CreatedById;
    }

    private IGitProviderClient ResolveProviderClient(string provider)
    {
        var client = _providerClients.FirstOrDefault(c =>
            string.Equals(c.Provider, provider, StringComparison.OrdinalIgnoreCase));

        return client ?? throw new DomainException($"No Git provider client registered for '{provider}'.");
    }

    private GitProviderContext BuildProviderContext(ProjectGitRepository repo)
    {
        var token = !string.IsNullOrWhiteSpace(repo.EncryptedAccessToken)
            ? _secretProtector.Unprotect(repo.EncryptedAccessToken)
            : null;

        return new GitProviderContext(
            repo.ApiBaseUrl,
            repo.WebBaseUrl,
            repo.RepositoryOwner,
            repo.RepositoryName,
            token);
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

    private async Task<TaskItem> GetAuthorizedTaskAsync(
        Guid taskId,
        Guid callerId,
        CancellationToken cancellationToken)
    {
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
        return task;
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

    private void EnsureCanManageProjectGit(Project project, Guid callerId)
    {
        if (_currentUserService.Roles.Contains(Roles.Administrator)) return;

        var member = project.Members.FirstOrDefault(m => m.UserId == callerId);
        if (member == null ||
            (member.ProjectRole != ProjectRoles.Owner && member.ProjectRole != ProjectRoles.Manager))
        {
            throw new ForbiddenException("Only Project Owners, Project Managers, or Administrators can manage Git repository connections.");
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

    private static ProjectGitRepositoryDto MapRepositoryDto(ProjectGitRepository repo) =>
        new(
            Id: repo.Id,
            ProjectId: repo.ProjectId,
            Provider: repo.Provider,
            RepositoryOwner: repo.RepositoryOwner,
            RepositoryName: repo.RepositoryName,
            RepositoryFullName: repo.RepositoryFullName,
            ApiBaseUrl: repo.ApiBaseUrl,
            WebBaseUrl: repo.WebBaseUrl,
            DefaultBranch: repo.DefaultBranch,
            HasAccessToken: repo.HasAccessToken,
            HasWebhookSecret: repo.HasWebhookSecret,
            WebhookPath: $"/api/v1/webhooks/git/{repo.Provider.ToLowerInvariant()}/{repo.Id}",
            IsDefault: repo.IsDefault,
            IsActive: repo.IsActive,
            AutoLinkEnabled: repo.AutoLinkEnabled,
            AutoTransitionEnabled: repo.AutoTransitionEnabled,
            UseInReviewColumn: repo.UseInReviewColumn,
            LastSyncedAtUtc: repo.LastSyncedAtUtc,
            LastSyncError: repo.LastSyncError,
            CreatedAtUtc: repo.CreatedAtUtc
        );

    private static TaskGitBranchDto MapBranchDto(TaskGitBranch b) =>
        new(
            Id: b.Id,
            TaskId: b.TaskId,
            RepositoryId: b.RepositoryId,
            RepositoryFullName: b.Repository?.RepositoryFullName ?? string.Empty,
            BranchName: b.BranchName,
            BaseBranch: b.BaseBranch,
            BranchUrl: b.BranchUrl,
            CreatedByUserId: b.CreatedByUserId,
            CreatedAtUtc: b.CreatedAtUtc
        );

    private static TaskPullRequestDto MapPullRequestDto(TaskPullRequestLink link)
    {
        var pr = link.PullRequest!;
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
            LinkSource: link.LinkSource,
            OpenedAtUtc: pr.OpenedAtUtc,
            MergedAtUtc: pr.MergedAtUtc,
            ClosedAtUtc: pr.ClosedAtUtc,
            LastSyncedAtUtc: pr.LastSyncedAtUtc
        );
    }
}
