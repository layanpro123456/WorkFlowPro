using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Application.Features.Admin.Dtos;

namespace WorkFlowPro.Application.Features.Admin.Services;

public interface IAdminAppService
{
    Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(
        string? search,
        string? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AdminUserSummaryDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken);

    Task<AdminUserSummaryDto> UpdateUserStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken);

    Task<AdminUserSummaryDto> UpdateUserRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRolesAsync(
        CancellationToken cancellationToken);

    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        AuditLogFilterParams filters,
        CancellationToken cancellationToken);
}
