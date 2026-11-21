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
    }
}
