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
}
