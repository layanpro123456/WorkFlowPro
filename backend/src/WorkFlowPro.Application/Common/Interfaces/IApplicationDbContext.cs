using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Domain.Entities;

namespace WorkFlowPro.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectMember> ProjectMembers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
