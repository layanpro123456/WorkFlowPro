using WorkFlowPro.Application.Common.Models;

namespace WorkFlowPro.Application.Features.Admin.Dtos;

public record AdminUserSummaryDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IList<string> Roles,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateUserRequest(
    string Email,
    string DisplayName,
    string? Password,
    IList<string> Roles
);

public record UpdateUserStatusRequest(
    bool IsActive
);

public record UpdateUserRolesRequest(
    IList<string> Roles
);

public record AuditLogDto(
    Guid Id,
    Guid? ActorId,
    string? ActorDisplayName,
    string? ActorEmail,
    string Action,
    string EntityType,
    Guid? EntityId,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? MetadataJson
);

public record AuditLogFilterParams(
    string? Action = null,
    string? EntityType = null,
    Guid? ActorId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 20
);
