using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Admin.Dtos;
using WorkFlowPro.Domain.Constants;

namespace WorkFlowPro.Application.Features.Admin.Services;

public class AdminAppService : IAdminAppService
{
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public AdminAppService(
        IIdentityService identityService,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _identityService = identityService;
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(
        string? search,
        string? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var (items, totalCount) = await _identityService.ListUsersAsync(
            search,
            role,
            isActive,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(u => new AdminUserSummaryDto(
            u.Id,
            u.Email,
            u.DisplayName,
            u.IsActive,
            u.Roles,
            u.CreatedAtUtc,
            u.UpdatedAtUtc
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<AdminUserSummaryDto>(dtos, page, pageSize, totalCount, totalPages);
    }

    public async Task<AdminUserSummaryDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(request.DisplayName));
        }

        var roles = request.Roles?.ToList() ?? new List<string> { Roles.Employee };
        foreach (var r in roles)
        {
            if (!Roles.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Role '{r}' is not a valid system role.");
            }
        }

        var password = string.IsNullOrWhiteSpace(request.Password) ? "Pass1234!" : request.Password;

        var created = await _identityService.CreateUserAsync(
            request.Email.Trim(),
            request.DisplayName.Trim(),
            password,
            roles,
            cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.UserCreated,
            "User",
            created.Id,
            new { email = created.Email, displayName = created.DisplayName, roles = created.Roles },
            cancellationToken);

        return new AdminUserSummaryDto(
            created.Id,
            created.Email,
            created.DisplayName,
            created.IsActive,
            created.Roles,
            created.CreatedAtUtc,
            created.UpdatedAtUtc
        );
    }

    public async Task<AdminUserSummaryDto> UpdateUserStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId.HasValue && _currentUserService.UserId.Value == userId && !request.IsActive)
        {
            throw new InvalidOperationException("You cannot deactivate your own administrator account.");
        }

        if (!request.IsActive)
        {
            var isSoleAdmin = await _identityService.IsSoleActiveAdminAsync(userId, cancellationToken);
            if (isSoleAdmin)
            {
                throw new InvalidOperationException("Cannot deactivate the sole active administrator.");
            }
        }

        var updated = await _identityService.SetUserStatusAsync(userId, request.IsActive, cancellationToken);

        // Revoke active refresh tokens on deactivation
        if (!request.IsActive)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(DateTime.UtcNow);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        await _auditService.RecordAsync(
            request.IsActive ? AuditActions.UserActivated : AuditActions.UserDeactivated,
            "User",
            userId,
            new { isActive = request.IsActive },
            cancellationToken);

        return new AdminUserSummaryDto(
            updated.Id,
            updated.Email,
            updated.DisplayName,
            updated.IsActive,
            updated.Roles,
            updated.CreatedAtUtc,
            updated.UpdatedAtUtc
        );
    }

    public async Task<AdminUserSummaryDto> UpdateUserRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Roles == null || request.Roles.Count == 0)
        {
            throw new ArgumentException("At least one role must be assigned to the user.");
        }

        foreach (var r in request.Roles)
        {
            if (!Roles.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Role '{r}' is not a valid system role.");
            }
        }

        var targetUser = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        if (targetUser == null)
        {
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        }

        var hasAdminRoleCurrently = targetUser.Value.Roles.Contains(Roles.Administrator, StringComparer.OrdinalIgnoreCase);
        var willHaveAdminRole = request.Roles.Contains(Roles.Administrator, StringComparer.OrdinalIgnoreCase);

        if (hasAdminRoleCurrently && !willHaveAdminRole)
        {
            var isSoleAdmin = await _identityService.IsSoleActiveAdminAsync(userId, cancellationToken);
            if (isSoleAdmin)
            {
                throw new InvalidOperationException("Cannot remove administrator role from the sole active administrator.");
            }
        }

        var updated = await _identityService.SetUserRolesAsync(userId, request.Roles, cancellationToken);

        await _auditService.RecordAsync(
            AuditActions.UserRolesUpdated,
            "User",
            userId,
            new { roles = request.Roles },
            cancellationToken);

        return new AdminUserSummaryDto(
            updated.Id,
            updated.Email,
            updated.DisplayName,
            updated.IsActive,
            updated.Roles,
            updated.CreatedAtUtc,
            updated.UpdatedAtUtc
        );
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken)
    {
        return await _identityService.GetAllRolesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        AuditLogFilterParams filters,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(filters.PageSize, 1, 100);
        var page = Math.Max(1, filters.Page);

        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Action) && filters.Action != "All")
        {
            query = query.Where(a => a.Action == filters.Action);
        }

        if (!string.IsNullOrWhiteSpace(filters.EntityType) && filters.EntityType != "All")
        {
            query = query.Where(a => a.EntityType == filters.EntityType);
        }

        if (filters.ActorId.HasValue)
        {
            query = query.Where(a => a.ActorId == filters.ActorId.Value);
        }

        if (filters.StartDate.HasValue)
        {
            query = query.Where(a => a.OccurredAtUtc >= filters.StartDate.Value);
        }

        if (filters.EndDate.HasValue)
        {
            query = query.Where(a => a.OccurredAtUtc <= filters.EndDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pagedLogs = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actorIds = pagedLogs
            .Where(a => a.ActorId.HasValue)
            .Select(a => a.ActorId!.Value)
            .Distinct()
            .ToList();

        var userSummaries = await _identityService.GetUsersSummaryAsync(actorIds, cancellationToken);

        var dtos = pagedLogs.Select(a =>
        {
            string? actorDisplayName = null;
            string? actorEmail = null;

            if (a.ActorId.HasValue && userSummaries.TryGetValue(a.ActorId.Value, out var summary))
            {
                actorDisplayName = summary.DisplayName;
                actorEmail = summary.Email;
            }

            return new AuditLogDto(
                a.Id,
                a.ActorId,
                actorDisplayName,
                actorEmail,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OccurredAtUtc,
                a.CorrelationId,
                a.MetadataJson
            );
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<AuditLogDto>(dtos, page, pageSize, totalCount, totalPages);
    }
}
