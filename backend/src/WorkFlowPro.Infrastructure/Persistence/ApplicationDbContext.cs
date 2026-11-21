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
    }
}
