using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Domain.Entities;

namespace WorkFlowPro.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectMember> ProjectMembers { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<TaskComment> TaskComments { get; }
    DbSet<TaskStatusHistory> TaskStatusHistories { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<TicketStatusHistory> TicketStatusHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
