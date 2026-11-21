using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;

namespace WorkFlowPro.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<(bool Success, bool IsInactive, Guid UserId, string Email, string DisplayName, IList<string> Roles)> CheckCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return (false, false, Guid.Empty, string.Empty, string.Empty, Array.Empty<string>());
        }

        if (!user.IsActive)
        {
            return (false, true, user.Id, user.Email ?? string.Empty, user.DisplayName, Array.Empty<string>());
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return (false, true, user.Id, user.Email ?? string.Empty, user.DisplayName, Array.Empty<string>());
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return (false, false, user.Id, user.Email ?? string.Empty, user.DisplayName, Array.Empty<string>());
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return (true, false, user.Id, user.Email ?? string.Empty, user.DisplayName, roles);
    }

    public async Task<(Guid UserId, string Email, string DisplayName, IList<string> Roles)?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return (user.Id, user.Email ?? string.Empty, user.DisplayName, roles);
    }

    public async Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user != null && user.IsActive;
    }

    public async Task<IList<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Array.Empty<string>();

        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IDictionary<Guid, (string DisplayName, string Email)>> GetUsersSummaryAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = userIds.Distinct().ToList();
        if (!distinctIds.Any())
        {
            return new Dictionary<Guid, (string DisplayName, string Email)>();
        }

        var users = await _userManager.Users
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, Email = u.Email ?? string.Empty })
            .ToDictionaryAsync(u => u.Id, u => (u.DisplayName, u.Email), cancellationToken);

        return users;
    }

    public async Task<IReadOnlyList<(Guid UserId, string DisplayName, string Email, IList<string> Roles)>> GetUsersInRolesAsync(
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        var roleList = roles.ToList();
        var result = new List<(Guid UserId, string DisplayName, string Email, IList<string> Roles)>();

        var activeUsers = await _userManager.Users
            .Where(u => u.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var user in activeUsers)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any(r => roleList.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                result.Add((user.Id, user.DisplayName, user.Email ?? string.Empty, userRoles));
            }
        }

        return result;
    }
}
