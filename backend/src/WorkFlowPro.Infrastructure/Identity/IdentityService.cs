using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;

namespace WorkFlowPro.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
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
            .OrderBy(u => u.UserName)
            .ThenBy(u => u.Id)
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

    public async Task<(IReadOnlyList<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> Items, int TotalCount)> ListUsersAsync(
        string? search,
        string? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.DisplayName.Contains(term) || (u.Email != null && u.Email.Contains(term)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var usersList = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userRoleList = new List<(ApplicationUser User, IList<string> Roles)>();
        foreach (var u in usersList)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (string.IsNullOrWhiteSpace(role) || role == "All" || roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                userRoleList.Add((u, roles));
            }
        }

        var totalCount = userRoleList.Count;
        var paged = userRoleList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => (x.User.Id, x.User.Email ?? string.Empty, x.User.DisplayName, x.User.IsActive, x.Roles, x.User.CreatedAtUtc, x.User.UpdatedAtUtc))
            .ToList();

        return (paged, totalCount);
    }

    public async Task<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> CreateUserAsync(
        string email,
        string displayName,
        string password,
        IList<string> roles,
        CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            throw new InvalidOperationException($"User with email '{email}' already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        if (roles != null && roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, roles);
        }

        var assignedRoles = await _userManager.GetRolesAsync(user);
        return (user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive, assignedRoles, user.CreatedAtUtc, user.UpdatedAtUtc);
    }

    public async Task<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> SetUserStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        }

        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return (user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive, roles, user.CreatedAtUtc, user.UpdatedAtUtc);
    }

    public async Task<(Guid Id, string Email, string DisplayName, bool IsActive, IList<string> Roles, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)> SetUserRolesAsync(
        Guid userId,
        IList<string> roles,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(roles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToAdd = roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (rolesToRemove.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Any())
        {
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        user.UpdatedAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var updatedRoles = await _userManager.GetRolesAsync(user);
        return (user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive, updatedRoles, user.CreatedAtUtc, user.UpdatedAtUtc);
    }

    public async Task<bool> IsSoleActiveAdminAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync(Domain.Constants.Roles.Administrator);
        var activeAdmins = adminUsers.Where(u => u.IsActive).ToList();

        return activeAdmins.Count == 1 && activeAdmins[0].Id == userId;
    }

    public async Task<IReadOnlyList<string>> GetAllRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync(cancellationToken);
        return roles.Count > 0 ? roles : Domain.Constants.Roles.All;
    }

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user?.Id;
    }
}
