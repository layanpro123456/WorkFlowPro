using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkFlowPro.Application.Common.Interfaces;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Infrastructure.Identity;

namespace WorkFlowPro.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskStatusHistory> TaskStatusHistories => Set<TaskStatusHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.DisplayName)
                .HasMaxLength(150)
                .IsRequired();

            b.Property(u => u.IsActive)
                .HasDefaultValue(true);

            b.Property(u => u.CreatedAtUtc)
                .IsRequired();
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(t => t.Id);

            b.Property(t => t.TokenHash)
                .HasMaxLength(256)
                .IsRequired();

            b.HasIndex(t => t.TokenHash)
                .IsUnique();

            b.Property(t => t.DeviceLabel)
                .HasMaxLength(100);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.HasKey(p => p.Id);

            b.Property(p => p.ProjectCode)
                .HasMaxLength(30)
                .IsRequired();

            b.HasIndex(p => p.ProjectCode)
                .IsUnique();

            b.Property(p => p.Name)
                .HasMaxLength(150)
                .IsRequired();

            b.Property(p => p.Description);

            b.Property(p => p.Status)
                .HasMaxLength(20)
                .IsRequired();

            b.HasIndex(p => p.Status);

            b.Property(p => p.StartDate);
            b.Property(p => p.TargetDate);

            b.HasIndex(p => p.CreatedById);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(p => p.Members)
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectMember>(b =>
        {
            b.ToTable("project_members");
            b.HasKey(m => new { m.ProjectId, m.UserId });

            b.Property(m => m.ProjectRole)
                .HasMaxLength(20)
                .IsRequired();

            b.HasIndex(m => m.UserId);
            b.HasIndex(m => new { m.ProjectId, m.ProjectRole });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaskItem>(b =>
        {
            b.ToTable("tasks");
            b.HasKey(t => t.Id);

            b.Property(t => t.Title)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(t => t.Description);

            b.Property(t => t.Status)
                .HasMaxLength(20)
                .IsRequired();

            b.Property(t => t.Priority)
                .HasMaxLength(20)
                .IsRequired();

            b.Property(t => t.DueAtUtc);
            b.Property(t => t.EstimateMinutes);

            b.Property(t => t.Version)
                .IsRequired()
                .HasDefaultValue(1)
                .IsConcurrencyToken();

            b.HasIndex(t => new { t.ProjectId, t.Status });
            b.HasIndex(t => new { t.AssigneeId, t.Status });
            b.HasIndex(t => t.DueAtUtc);
            b.HasIndex(t => t.CreatedAtUtc);

            b.HasOne(t => t.Project)
                .WithMany()
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(t => t.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(t => t.StatusHistory)
                .WithOne(h => h.Task)
                .HasForeignKey(h => h.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaskComment>(b =>
        {
            b.ToTable("task_comments");
            b.HasKey(c => c.Id);

            b.Property(c => c.Body)
                .IsRequired();

            b.HasIndex(c => new { c.TaskId, c.CreatedAtUtc });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaskStatusHistory>(b =>
        {
            b.ToTable("task_status_histories");
            b.HasKey(h => h.Id);

            b.Property(h => h.FromStatus)
                .HasMaxLength(20);

            b.Property(h => h.ToStatus)
                .HasMaxLength(20)
                .IsRequired();

            b.Property(h => h.Comment);

            b.HasIndex(h => new { h.TaskId, h.ChangedAtUtc });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
