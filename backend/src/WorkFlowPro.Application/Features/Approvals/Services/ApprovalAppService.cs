using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Approvals.Dtos;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Domain.Exceptions;

namespace WorkFlowPro.Application.Features.Approvals.Services;

public class ApprovalAppService : IApprovalAppService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IValidator<CreateWorkflowDefinitionRequest> _createDefValidator;
    private readonly IValidator<UpdateWorkflowDefinitionRequest> _updateDefValidator;
    private readonly IValidator<SubmitApprovalRequest> _submitRequestValidator;
    private readonly IValidator<RecordDecisionRequest> _recordDecisionValidator;
    private readonly IValidator<CancelApprovalRequest> _cancelRequestValidator;

    private static readonly string[] ManagerOrAdminRoles = { Roles.Manager, Roles.Administrator };

    public ApprovalAppService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IValidator<CreateWorkflowDefinitionRequest> createDefValidator,
        IValidator<UpdateWorkflowDefinitionRequest> updateDefValidator,
        IValidator<SubmitApprovalRequest> submitRequestValidator,
        IValidator<RecordDecisionRequest> recordDecisionValidator,
        IValidator<CancelApprovalRequest> cancelRequestValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _createDefValidator = createDefValidator;
        _updateDefValidator = updateDefValidator;
        _submitRequestValidator = submitRequestValidator;
        _recordDecisionValidator = recordDecisionValidator;
        _cancelRequestValidator = cancelRequestValidator;
    }

    private Guid RequireCallerId() =>
        _currentUserService.UserId ?? throw new UnauthorizedAccessException("Authentication required.");

    private bool IsManagerOrAdmin() =>
        _currentUserService.Roles.Any(r => ManagerOrAdminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    #region Workflow Definitions

    public async Task<IReadOnlyList<WorkflowDefinitionSummaryDto>> ListDefinitionsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _context.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.Steps)
            .AsQueryable();

        if (!includeInactive || !IsManagerOrAdmin())
        {
            query = query.Where(w => w.IsActive);
        }

        var definitions = await query
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

        return definitions.Select(w => new WorkflowDefinitionSummaryDto(
            w.Id,
            w.Name,
            w.Description,
            w.IsActive,
            w.Steps.Count,
            w.CreatedById,
            w.CreatedAtUtc,
            w.UpdatedAtUtc
        )).ToList();
    }

    public async Task<WorkflowDefinitionDetailDto> GetDefinitionByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var definition = await _context.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (definition == null)
        {
            throw new KeyNotFoundException($"Workflow definition '{id}' not found.");
        }

        var userIdsToFetch = new HashSet<Guid> { definition.CreatedById };
        foreach (var step in definition.Steps)
        {
            if (step.ApproverUserId.HasValue)
            {
                userIdsToFetch.Add(step.ApproverUserId.Value);
            }
        }

        var userSummaries = await _identityService.GetUsersSummaryAsync(userIdsToFetch, cancellationToken);

        var createdByName = userSummaries.TryGetValue(definition.CreatedById, out var creator)
            ? creator.DisplayName
            : "Unknown User";

        var stepDtos = definition.Steps
            .OrderBy(s => s.StepOrder)
            .Select(s =>
            {
                string? approverName = null;
                if (s.ApproverUserId.HasValue && userSummaries.TryGetValue(s.ApproverUserId.Value, out var approver))
                {
                    approverName = approver.DisplayName;
                }

                return new WorkflowStepDto(
                    s.Id,
                    s.StepOrder,
                    s.Name,
                    s.ApproverRole,
                    s.ApproverUserId,
                    approverName
                );
            }).ToList();

        return new WorkflowDefinitionDetailDto(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.IsActive,
            definition.CreatedById,
            createdByName,
            definition.CreatedAtUtc,
            definition.UpdatedAtUtc,
            stepDtos
        );
    }

    public async Task<WorkflowDefinitionDetailDto> CreateDefinitionAsync(
        CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        if (!IsManagerOrAdmin())
        {
            throw new ForbiddenException("Only managers and administrators can create workflow definitions.");
        }

        await _createDefValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Verify that any explicit approver users exist and are active
        var explicitUserIds = request.Steps
            .Where(s => s.ApproverUserId.HasValue)
            .Select(s => s.ApproverUserId!.Value)
            .Distinct()
            .ToList();

        foreach (var userId in explicitUserIds)
        {
            var isActive = await _identityService.IsUserActiveAsync(userId, cancellationToken);
            if (!isActive)
            {
                throw new ValidationException($"User '{userId}' is inactive or does not exist.");
            }
        }

        var definition = new WorkflowDefinition(request.Name, request.Description, callerId, isActive: true);

        var steps = request.Steps.Select(s =>
            new WorkflowStep(definition.Id, s.StepOrder, s.Name, s.ApproverRole, s.ApproverUserId)
        );

        definition.SetSteps(steps);

        _context.WorkflowDefinitions.Add(definition);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetDefinitionByIdAsync(definition.Id, cancellationToken);
    }

    public async Task<WorkflowDefinitionDetailDto> UpdateDefinitionAsync(
        Guid id,
        UpdateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        RequireCallerId();
        if (!IsManagerOrAdmin())
        {
            throw new ForbiddenException("Only managers and administrators can update workflow definitions.");
        }

        await _updateDefValidator.ValidateAndThrowAsync(request, cancellationToken);

        var definition = await _context.WorkflowDefinitions
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (definition == null)
        {
            throw new KeyNotFoundException($"Workflow definition '{id}' not found.");
        }

        // Verify explicit approver users
        var explicitUserIds = request.Steps
            .Where(s => s.ApproverUserId.HasValue)
            .Select(s => s.ApproverUserId!.Value)
            .Distinct()
            .ToList();

        foreach (var userId in explicitUserIds)
        {
            var isActive = await _identityService.IsUserActiveAsync(userId, cancellationToken);
            if (!isActive)
            {
                throw new ValidationException($"User '{userId}' is inactive or does not exist.");
            }
        }

        definition.Update(request.Name, request.Description, request.IsActive);

        // Remove old steps
        _context.WorkflowSteps.RemoveRange(definition.Steps);

        var newSteps = request.Steps.Select(s =>
            new WorkflowStep(definition.Id, s.StepOrder, s.Name, s.ApproverRole, s.ApproverUserId)
        ).ToList();

        definition.SetSteps(newSteps);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetDefinitionByIdAsync(definition.Id, cancellationToken);
    }

    #endregion

    #region Approval Requests

    public async Task<ApprovalRequestSummaryDto> SubmitRequestAsync(
        SubmitApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await _submitRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var definition = await _context.WorkflowDefinitions
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowDefinitionId, cancellationToken);

        if (definition == null || !definition.IsActive)
        {
            throw new ValidationException("The selected workflow definition does not exist or is inactive.");
        }

        if (definition.Steps.Count == 0)
        {
            throw new ValidationException("The selected workflow definition has no approval steps configured.");
        }

        var approvalRequest = new ApprovalRequest(
            definition.Id,
            callerId,
            request.Title,
            request.Description
        );

        // Sequential human-friendly request number
        var maxNumber = await _context.ApprovalRequests
            .Select(r => (long?)r.RequestNumber)
            .MaxAsync(cancellationToken) ?? 1000;

        approvalRequest.AssignRequestNumber(maxNumber + 1);

        // Snapshot steps with concrete resolved approver
        var orderedSteps = definition.Steps.OrderBy(s => s.StepOrder).ToList();
        var overrides = request.StepApproverOverrides?.ToDictionary(o => o.StepOrder, o => o.ApproverUserId)
            ?? new Dictionary<int, Guid>();

        foreach (var step in orderedSteps)
        {
            Guid resolvedApproverId;

            if (step.ApproverUserId.HasValue)
            {
                resolvedApproverId = step.ApproverUserId.Value;
                var isActive = await _identityService.IsUserActiveAsync(resolvedApproverId, cancellationToken);
                if (!isActive)
                {
                    throw new ValidationException($"Designated approver for step {step.StepOrder} ('{step.Name}') is inactive or invalid.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(step.ApproverRole))
            {
                var role = step.ApproverRole;

                // Check for explicit requester override
                if (overrides.TryGetValue(step.StepOrder, out var overrideUserId))
                {
                    var eligibleUsers = await _identityService.GetUsersInRolesAsync(new[] { role }, cancellationToken);
                    var matched = eligibleUsers.FirstOrDefault(u => u.UserId == overrideUserId);
                    if (matched.UserId == Guid.Empty)
                    {
                        throw new ValidationException(
                            $"Selected approver for step {step.StepOrder} is not an active user with required role '{role}'.");
                    }
                    resolvedApproverId = matched.UserId;
                }
                else
                {
                    // Deterministic fallback: pick earliest active user alphabetically by UserName
                    var eligibleUsers = await _identityService.GetUsersInRolesAsync(new[] { role }, cancellationToken);
                    if (eligibleUsers.Count == 0)
                    {
                        throw new ValidationException(
                            $"No active users found with required role '{role}' for step {step.StepOrder} ('{step.Name}').");
                    }
                    resolvedApproverId = eligibleUsers[0].UserId;
                }
            }
            else
            {
                throw new ValidationException($"Step {step.StepOrder} has no valid approver selector.");
            }

            approvalRequest.AddStepSnapshot(step.StepOrder, step.Name, resolvedApproverId);
        }

        // Create notification for the first approver
        var firstStep = approvalRequest.Steps.FirstOrDefault(s => s.StepOrder == approvalRequest.CurrentStepOrder);
        if (firstStep != null)
        {
            _context.Notifications.Add(new InAppNotification(
                firstStep.ApproverUserId,
                NotificationTypes.ApprovalActionRequired,
                $"Action Required: Approval #{approvalRequest.RequestNumber}",
                $"Approval Request #{approvalRequest.RequestNumber} ('{approvalRequest.Title}') requires your review for Step {firstStep.StepOrder}: {firstStep.Name}.",
                "Approval",
                approvalRequest.Id
            ));
        }

        _context.ApprovalRequests.Add(approvalRequest);
        await _context.SaveChangesAsync(cancellationToken);

        // Fetch user display names for summary response
        var userIds = new HashSet<Guid> { callerId };
        var currentStep = approvalRequest.Steps.FirstOrDefault(s => s.StepOrder == approvalRequest.CurrentStepOrder);
        if (currentStep != null)
        {
            userIds.Add(currentStep.ApproverUserId);
        }

        var summaries = await _identityService.GetUsersSummaryAsync(userIds, cancellationToken);
        var requesterName = summaries.TryGetValue(callerId, out var rInfo) ? rInfo.DisplayName : "Requester";
        string? currentApproverName = null;
        if (currentStep != null && summaries.TryGetValue(currentStep.ApproverUserId, out var aInfo))
        {
            currentApproverName = aInfo.DisplayName;
        }

        return new ApprovalRequestSummaryDto(
            approvalRequest.Id,
            approvalRequest.RequestNumber,
            definition.Id,
            definition.Name,
            approvalRequest.RequesterId,
            requesterName,
            approvalRequest.Title,
            approvalRequest.Status,
            approvalRequest.CurrentStepOrder,
            currentStep?.Name,
            currentStep?.ApproverUserId,
            currentApproverName,
            approvalRequest.Version,
            approvalRequest.SubmittedAtUtc,
            approvalRequest.CompletedAtUtc
        );
    }

    public async Task<PagedResult<ApprovalRequestSummaryDto>> ListRequestsAsync(
        string? status,
        bool? inboxOnly,
        bool? myRequestsOnly,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isManagerOrAdmin = IsManagerOrAdmin();

        var query = _context.ApprovalRequests
            .AsNoTracking()
            .Include(r => r.WorkflowDefinition)
            .Include(r => r.Steps)
            .AsQueryable();

        // Scope visibility
        if (inboxOnly == true)
        {
            // Inbox: Requests in Pending status where caller is designated approver on the current step
            query = query.Where(r =>
                r.Status == ApprovalRequestStatuses.Pending &&
                r.CurrentStepOrder.HasValue &&
                r.Steps.Any(s => s.StepOrder == r.CurrentStepOrder && s.ApproverUserId == callerId));
        }
        else if (myRequestsOnly == true)
        {
            query = query.Where(r => r.RequesterId == callerId);
        }
        else if (!isManagerOrAdmin)
        {
            // Regular employees see requests they submitted OR where they are an approver on any step
            query = query.Where(r =>
                r.RequesterId == callerId ||
                r.Steps.Any(s => s.ApproverUserId == callerId));
        }

        if (!string.IsNullOrWhiteSpace(status) && ApprovalRequestStatuses.IsValid(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            if (long.TryParse(searchLower.TrimStart('#'), out var reqNum))
            {
                query = query.Where(r => r.RequestNumber == reqNum || r.Title.ToLower().Contains(searchLower));
            }
            else
            {
                query = query.Where(r => r.Title.ToLower().Contains(searchLower));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var requests = await query
            .OrderByDescending(r => r.SubmittedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Resolve names
        var userIdsToFetch = new HashSet<Guid>();
        foreach (var r in requests)
        {
            userIdsToFetch.Add(r.RequesterId);
            var currentStep = r.Steps.FirstOrDefault(s => s.StepOrder == r.CurrentStepOrder);
            if (currentStep != null)
            {
                userIdsToFetch.Add(currentStep.ApproverUserId);
            }
        }

        var userSummaries = await _identityService.GetUsersSummaryAsync(userIdsToFetch, cancellationToken);

        var items = requests.Select(r =>
        {
            var requesterName = userSummaries.TryGetValue(r.RequesterId, out var req) ? req.DisplayName : "Requester";
            var currentStep = r.Steps.FirstOrDefault(s => s.StepOrder == r.CurrentStepOrder);
            string? currentApproverName = null;
            if (currentStep != null && userSummaries.TryGetValue(currentStep.ApproverUserId, out var app))
            {
                currentApproverName = app.DisplayName;
            }

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
                currentApproverName,
                r.Version,
                r.SubmittedAtUtc,
                r.CompletedAtUtc
            );
        }).ToList();

        return new PagedResult<ApprovalRequestSummaryDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<ApprovalRequestDetailDto> GetRequestByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        var isManagerOrAdmin = IsManagerOrAdmin();

        var request = await _context.ApprovalRequests
            .AsNoTracking()
            .Include(r => r.WorkflowDefinition)
            .Include(r => r.Steps)
                .ThenInclude(s => s.Decision)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null)
        {
            throw new KeyNotFoundException($"Approval request '{id}' not found.");
        }

        // RESOURCE CONCEALMENT: Caller must be requester, an approver on any step, or Manager/Admin
        var isParticipant = request.RequesterId == callerId || request.Steps.Any(s => s.ApproverUserId == callerId);
        if (!isParticipant && !isManagerOrAdmin)
        {
            throw new KeyNotFoundException($"Approval request '{id}' not found.");
        }

        var userIdsToFetch = new HashSet<Guid> { request.RequesterId };
        foreach (var s in request.Steps)
        {
            userIdsToFetch.Add(s.ApproverUserId);
            if (s.Decision != null)
            {
                userIdsToFetch.Add(s.Decision.ApproverId);
            }
        }

        var userSummaries = await _identityService.GetUsersSummaryAsync(userIdsToFetch, cancellationToken);

        (string DisplayName, string Email) requesterInfo = userSummaries.TryGetValue(request.RequesterId, out var req)
            ? req
            : ("Requester", string.Empty);

        var currentStep = request.Steps.FirstOrDefault(s => s.StepOrder == request.CurrentStepOrder);
        string? currentApproverName = null;
        if (currentStep != null && userSummaries.TryGetValue(currentStep.ApproverUserId, out var curApp))
        {
            currentApproverName = curApp.DisplayName;
        }

        var stepDtos = request.Steps
            .OrderBy(s => s.StepOrder)
            .Select(s =>
            {
                (string DisplayName, string Email) approver = userSummaries.TryGetValue(s.ApproverUserId, out var app)
                    ? app
                    : ("Approver", string.Empty);

                ApprovalDecisionDto? decisionDto = null;
                if (s.Decision != null)
                {
                    var decider = userSummaries.TryGetValue(s.Decision.ApproverId, out var dec)
                        ? dec.DisplayName
                        : "Decider";

                    decisionDto = new ApprovalDecisionDto(
                        s.Decision.Id,
                        s.Decision.ApproverId,
                        decider,
                        s.Decision.Decision,
                        s.Decision.Comment,
                        s.Decision.DecidedAtUtc
                    );
                }

                return new ApprovalRequestStepDto(
                    s.Id,
                    s.StepOrder,
                    s.Name,
                    s.ApproverUserId,
                    approver.DisplayName,
                    approver.Email,
                    s.Status,
                    s.DecidedAtUtc,
                    decisionDto
                );
            }).ToList();

        return new ApprovalRequestDetailDto(
            request.Id,
            request.RequestNumber,
            request.WorkflowDefinitionId,
            request.WorkflowDefinition?.Name ?? "Workflow",
            request.RequesterId,
            requesterInfo.DisplayName,
            requesterInfo.Email,
            request.Title,
            request.Description,
            request.Status,
            request.CurrentStepOrder,
            currentStep?.Name,
            currentStep?.ApproverUserId,
            currentApproverName,
            request.Version,
            request.SubmittedAtUtc,
            request.CompletedAtUtc,
            stepDtos
        );
    }

    public async Task<ApprovalRequestDetailDto> RecordDecisionAsync(
        Guid id,
        RecordDecisionRequest requestDto,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await _recordDecisionValidator.ValidateAndThrowAsync(requestDto, cancellationToken);

        var request = await _context.ApprovalRequests
            .Include(r => r.Steps)
                .ThenInclude(s => s.Decision)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null)
        {
            throw new KeyNotFoundException($"Approval request '{id}' not found.");
        }

        // Authorization and step invariants enforced inside domain entity
        var decision = request.ProcessDecision(
            callerId,
            requestDto.Decision,
            requestDto.Comment,
            requestDto.ExpectedVersion
        );

        // Add decision to context explicitly to avoid EF Core untracked entity issues
        _context.ApprovalDecisions.Add(decision);

        if (decision.Decision == ApprovalDecisions.Approved)
        {
            if (request.Status == ApprovalRequestStatuses.Approved)
            {
                _context.Notifications.Add(new InAppNotification(
                    request.RequesterId,
                    NotificationTypes.ApprovalCompleted,
                    $"Approval Request #{request.RequestNumber} Approved",
                    $"Your approval request #{request.RequestNumber} ('{request.Title}') has been fully approved.",
                    "Approval",
                    request.Id
                ));
            }
            else
            {
                var nextStep = request.Steps.FirstOrDefault(s => s.StepOrder == request.CurrentStepOrder);
                if (nextStep != null)
                {
                    _context.Notifications.Add(new InAppNotification(
                        nextStep.ApproverUserId,
                        NotificationTypes.ApprovalActionRequired,
                        $"Action Required: Approval #{request.RequestNumber}",
                        $"Approval Request #{request.RequestNumber} ('{request.Title}') advanced to Step {nextStep.StepOrder}: {nextStep.Name} and requires your review.",
                        "Approval",
                        request.Id
                    ));
                }

                _context.Notifications.Add(new InAppNotification(
                    request.RequesterId,
                    NotificationTypes.ApprovalDecided,
                    $"Approval Request #{request.RequestNumber} Step Approved",
                    $"A step was approved on request #{request.RequestNumber}.",
                    "Approval",
                    request.Id
                ));
            }
        }
        else if (decision.Decision == ApprovalDecisions.Rejected)
        {
            _context.Notifications.Add(new InAppNotification(
                request.RequesterId,
                NotificationTypes.ApprovalRejected,
                $"Approval Request #{request.RequestNumber} Rejected",
                $"Your approval request #{request.RequestNumber} ('{request.Title}') was rejected.",
                "Approval",
                request.Id
            ));
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRequestByIdAsync(request.Id, cancellationToken);
    }

    public async Task<ApprovalRequestDetailDto> CancelRequestAsync(
        Guid id,
        CancelApprovalRequest requestDto,
        CancellationToken cancellationToken)
    {
        var callerId = RequireCallerId();
        await _cancelRequestValidator.ValidateAndThrowAsync(requestDto, cancellationToken);

        var request = await _context.ApprovalRequests
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null)
        {
            throw new KeyNotFoundException($"Approval request '{id}' not found.");
        }

        // Only requester or Admin can cancel
        var isAdmin = _currentUserService.Roles.Contains(Roles.Administrator, StringComparer.OrdinalIgnoreCase);
        if (request.RequesterId != callerId && !isAdmin)
        {
            throw new ForbiddenException("Only the requester or an administrator can cancel an approval request.");
        }

        request.Cancel(callerId, requestDto.ExpectedVersion);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRequestByIdAsync(request.Id, cancellationToken);
    }

    #endregion
}
