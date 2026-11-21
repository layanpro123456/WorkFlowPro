namespace WorkFlowPro.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Success, bool IsInactive, Guid UserId, string Email, string DisplayName, IList<string> Roles)> CheckCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<(Guid UserId, string Email, string DisplayName, IList<string> Roles)?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken);

    Task<IList<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken);

    Task<IDictionary<Guid, (string DisplayName, string Email)>> GetUsersSummaryAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid UserId, string DisplayName, string Email, IList<string> Roles)>> GetUsersInRolesAsync(
        IEnumerable<string> roles,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> Items, int TotalCount)> ListUsersAsync(
        string? search,
        string? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> CreateUserAsync(
        string email,
        string displayName,
        string password,
        IList<string> roles,
        CancellationToken cancellationToken);

    Task<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> SetUserStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken);

    Task<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> SetUserRolesAsync(
        Guid userId,
        IList<string> roles,
        CancellationToken cancellationToken);

    Task<bool> IsSoleActiveAdminAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAllRolesAsync(
        CancellationToken cancellationToken);
}
