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
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalRequestStep> ApprovalRequestSteps => Set<ApprovalRequestStep>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

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

        builder.Entity<SupportTicket>(b =>
        {
            b.ToTable("support_tickets");
            b.HasKey(t => t.Id);

            b.Property(t => t.TicketNumber)
                .ValueGeneratedOnAdd();

            b.HasIndex(t => t.TicketNumber)
                .IsUnique();

            b.Property(t => t.Subject)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(t => t.Description)
                .IsRequired();

            b.Property(t => t.Category)
                .HasMaxLength(50)
                .IsRequired();

            b.Property(t => t.Priority)
                .HasMaxLength(20)
                .IsRequired();

            b.Property(t => t.Status)
                .HasMaxLength(30)
                .IsRequired();

            b.HasIndex(t => new { t.RequesterId, t.Status });
            b.HasIndex(t => new { t.AssigneeId, t.Status });
            b.HasIndex(t => new { t.Status, t.Priority });
            b.HasIndex(t => t.CreatedAtUtc);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(t => t.RelatedProject)
                .WithMany()
                .HasForeignKey(t => t.RelatedProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(t => t.Messages)
                .WithOne(m => m.Ticket)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(t => t.StatusHistory)
                .WithOne(h => h.Ticket)
                .HasForeignKey(h => h.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TicketMessage>(b =>
        {
            b.ToTable("ticket_messages");
            b.HasKey(m => m.Id);

            b.Property(m => m.Body)
                .IsRequired();

            b.Property(m => m.IsInternalNote)
                .IsRequired();

            b.HasIndex(m => new { m.TicketId, m.CreatedAtUtc });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TicketStatusHistory>(b =>
        {
            b.ToTable("ticket_status_histories");
            b.HasKey(h => h.Id);

            b.Property(h => h.FromStatus)
                .HasMaxLength(30);

            b.Property(h => h.ToStatus)
                .HasMaxLength(30)
                .IsRequired();

            b.Property(h => h.Comment);

            b.HasIndex(h => new { h.TicketId, h.ChangedAtUtc });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(h => h.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkflowDefinition>(b =>
        {
            b.ToTable("workflow_definitions");
            b.HasKey(w => w.Id);

            b.Property(w => w.Name)
                .HasMaxLength(150)
                .IsRequired();

            b.Property(w => w.Description);

            b.Property(w => w.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            b.HasIndex(w => w.CreatedById);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(w => w.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(w => w.Steps)
                .WithOne(s => s.WorkflowDefinition)
                .HasForeignKey(s => s.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation(w => w.Steps)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<WorkflowStep>(b =>
        {
            b.ToTable("workflow_steps");
            b.HasKey(s => s.Id);

            b.Property(s => s.Name)
                .HasMaxLength(150)
                .IsRequired();

            b.Property(s => s.StepOrder)
                .IsRequired();

            b.Property(s => s.ApproverRole)
                .HasMaxLength(50);

            b.HasIndex(s => new { s.WorkflowDefinitionId, s.StepOrder })
                .IsUnique();

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovalRequest>(b =>
        {
            b.ToTable("approval_requests");
            b.HasKey(r => r.Id);

            b.Property(r => r.RequestNumber)
                .ValueGeneratedOnAdd();

            b.HasIndex(r => r.RequestNumber)
                .IsUnique();

            b.Property(r => r.Title)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(r => r.Description)
                .IsRequired();

            b.Property(r => r.Status)
                .HasMaxLength(20)
                .IsRequired();

            b.Property(r => r.CurrentStepOrder);

            b.Property(r => r.Version)
                .IsRequired()
                .HasDefaultValue(1)
                .IsConcurrencyToken();

            b.HasIndex(r => new { r.RequesterId, r.Status });
            b.HasIndex(r => r.Status);
            b.HasIndex(r => r.SubmittedAtUtc);

            b.HasOne(r => r.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(r => r.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(r => r.Steps)
                .WithOne(s => s.Request)
                .HasForeignKey(s => s.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation(r => r.Steps)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ApprovalRequestStep>(b =>
        {
            b.ToTable("approval_request_steps");
            b.HasKey(s => s.Id);

            b.Property(s => s.StepOrder)
                .IsRequired();

            b.Property(s => s.Name)
                .HasMaxLength(150)
                .IsRequired();

            b.Property(s => s.Status)
                .HasMaxLength(20)
                .IsRequired();

            b.HasIndex(s => new { s.ApprovalRequestId, s.StepOrder })
                .IsUnique();

            b.HasIndex(s => new { s.ApproverUserId, s.Status });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(s => s.Decision)
                .WithOne(d => d.Step)
                .HasForeignKey<ApprovalDecision>(d => d.ApprovalRequestStepId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApprovalDecision>(b =>
        {
            b.ToTable("approval_decisions");
            b.HasKey(d => d.Id);

            b.Property(d => d.Decision)
                .HasMaxLength(20)
                .IsRequired();

            b.Property(d => d.Comment);

            b.HasIndex(d => d.ApprovalRequestStepId)
                .IsUnique();

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(d => d.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
