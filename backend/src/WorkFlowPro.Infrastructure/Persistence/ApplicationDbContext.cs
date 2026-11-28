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
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ProjectGitRepository> ProjectGitRepositories => Set<ProjectGitRepository>();
    public DbSet<TaskGitBranch> TaskGitBranches => Set<TaskGitBranch>();
    public DbSet<GitPullRequest> GitPullRequests => Set<GitPullRequest>();
    public DbSet<TaskPullRequestLink> TaskPullRequestLinks => Set<TaskPullRequestLink>();
    public DbSet<GitWebhookDelivery> GitWebhookDeliveries => Set<GitWebhookDelivery>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<IssueLink> IssueLinks => Set<IssueLink>();
    public DbSet<ProjectBoardColumnConfig> BoardColumnConfigs => Set<ProjectBoardColumnConfig>();
    public DbSet<SprintBurndownSnapshot> SprintBurndownSnapshots => Set<SprintBurndownSnapshot>();

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

            b.Property(t => t.TaskNumber)
                .IsRequired()
                .HasDefaultValue(0);

            b.Property(t => t.TaskKey)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue(string.Empty);

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

            b.Property(t => t.IssueType)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Task");

            b.Property(t => t.StoryPoints);

            b.Property(t => t.BoardRank)
                .IsRequired()
                .HasDefaultValue(0);

            b.Property(t => t.DueAtUtc);
            b.Property(t => t.EstimateMinutes);

            b.Property(t => t.Version)
                .IsRequired()
                .HasDefaultValue(1)
                .IsConcurrencyToken();

            b.HasIndex(t => new { t.ProjectId, t.Status });
            b.HasIndex(t => new { t.ProjectId, t.SprintId, t.Status, t.BoardRank });
            b.HasIndex(t => new { t.ProjectId, t.IssueType });
            b.HasIndex(t => new { t.ProjectId, t.TaskNumber })
                .IsUnique()
                .HasFilter("\"TaskNumber\" > 0");
            b.HasIndex(t => t.TaskKey);
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

            b.HasMany(t => t.GitBranches)
                .WithOne(gb => gb.Task)
                .HasForeignKey(gb => gb.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(t => t.PullRequestLinks)
                .WithOne(l => l.Task)
                .HasForeignKey(l => l.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(t => t.Epic)
                .WithMany()
                .HasForeignKey(t => t.EpicId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(t => t.ParentTask)
                .WithMany(p => p.Subtasks)
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.Sprint)
                .WithMany()
                .HasForeignKey(t => t.SprintId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(t => t.OutgoingLinks)
                .WithOne(l => l.SourceTask)
                .HasForeignKey(l => l.SourceTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(t => t.IncomingLinks)
                .WithOne(l => l.TargetTask)
                .HasForeignKey(l => l.TargetTaskId)
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

        builder.Entity<InAppNotification>(b =>
        {
            b.ToTable("notifications");
            b.HasKey(n => n.Id);

            b.Property(n => n.Type)
                .HasMaxLength(60)
                .IsRequired();

            b.Property(n => n.Title)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(n => n.Message)
                .IsRequired();

            b.Property(n => n.EntityType)
                .HasMaxLength(50);

            b.Property(n => n.EntityId);

            b.Property(n => n.ReadAtUtc);

            b.HasIndex(n => new { n.RecipientId, n.ReadAtUtc, n.CreatedAtUtc });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
            b.HasKey(a => a.Id);

            b.Property(a => a.Action)
                .HasMaxLength(100)
                .IsRequired();

            b.Property(a => a.EntityType)
                .HasMaxLength(80)
                .IsRequired();

            b.Property(a => a.CorrelationId)
                .HasMaxLength(100);

            b.Property(a => a.MetadataJson)
                .HasColumnName("metadata");

            b.HasIndex(a => a.OccurredAtUtc);
            b.HasIndex(a => new { a.ActorId, a.OccurredAtUtc });
            b.HasIndex(a => new { a.EntityType, a.EntityId });

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.ActorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProjectGitRepository>(b =>
        {
            b.ToTable("project_git_repositories");
            b.HasKey(r => r.Id);

            b.Property(r => r.Provider).HasMaxLength(30).IsRequired();
            b.Property(r => r.RepositoryOwner).HasMaxLength(100).IsRequired();
            b.Property(r => r.RepositoryName).HasMaxLength(100).IsRequired();
            b.Property(r => r.RepositoryFullName).HasMaxLength(210).IsRequired();
            b.Property(r => r.ApiBaseUrl).HasMaxLength(300).IsRequired();
            b.Property(r => r.WebBaseUrl).HasMaxLength(300).IsRequired();
            b.Property(r => r.DefaultBranch).HasMaxLength(100).IsRequired();
            b.Property(r => r.LastSyncError).HasMaxLength(1000);

            b.HasIndex(r => new { r.ProjectId, r.Provider, r.RepositoryFullName }).IsUnique();
            b.HasIndex(r => r.ProjectId);

            b.HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaskGitBranch>(b =>
        {
            b.ToTable("task_git_branches");
            b.HasKey(gb => gb.Id);

            b.Property(gb => gb.BranchName).HasMaxLength(200).IsRequired();
            b.Property(gb => gb.BaseBranch).HasMaxLength(100).IsRequired();
            b.Property(gb => gb.BranchUrl).HasMaxLength(500).IsRequired();

            b.HasIndex(gb => new { gb.RepositoryId, gb.BranchName }).IsUnique();
            b.HasIndex(gb => gb.TaskId);

            b.HasOne(gb => gb.Repository)
                .WithMany()
                .HasForeignKey(gb => gb.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GitPullRequest>(b =>
        {
            b.ToTable("git_pull_requests");
            b.HasKey(pr => pr.Id);

            b.Property(pr => pr.Title).HasMaxLength(300).IsRequired();
            b.Property(pr => pr.State).HasMaxLength(20).IsRequired();
            b.Property(pr => pr.SourceBranch).HasMaxLength(200).IsRequired();
            b.Property(pr => pr.TargetBranch).HasMaxLength(200).IsRequired();
            b.Property(pr => pr.AuthorLogin).HasMaxLength(100).IsRequired();
            b.Property(pr => pr.AuthorAvatarUrl).HasMaxLength(500);
            b.Property(pr => pr.HtmlUrl).HasMaxLength(500).IsRequired();
            b.Property(pr => pr.ReviewStatus).HasMaxLength(30).IsRequired();
            b.Property(pr => pr.CheckStatus).HasMaxLength(30).IsRequired();

            b.HasIndex(pr => new { pr.RepositoryId, pr.PrNumber }).IsUnique();
            b.HasIndex(pr => new { pr.RepositoryId, pr.SourceBranch });

            b.HasOne(pr => pr.Repository)
                .WithMany()
                .HasForeignKey(pr => pr.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(pr => pr.TaskLinks)
                .WithOne(l => l.PullRequest)
                .HasForeignKey(l => l.PullRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaskPullRequestLink>(b =>
        {
            b.ToTable("task_pull_request_links");
            b.HasKey(l => l.Id);

            b.Property(l => l.LinkSource).HasMaxLength(30).IsRequired();

            b.HasIndex(l => new { l.TaskId, l.PullRequestId }).IsUnique();
            b.HasIndex(l => l.PullRequestId);
        });

        builder.Entity<GitWebhookDelivery>(b =>
        {
            b.ToTable("git_webhook_deliveries");
            b.HasKey(d => d.Id);

            b.Property(d => d.Provider).HasMaxLength(30).IsRequired();
            b.Property(d => d.DeliveryId).HasMaxLength(150).IsRequired();
            b.Property(d => d.EventType).HasMaxLength(50).IsRequired();
            b.Property(d => d.PayloadHash).HasMaxLength(64).IsRequired();
            b.Property(d => d.Status).HasMaxLength(20).IsRequired();
            b.Property(d => d.ErrorMessage).HasMaxLength(1000);

            b.HasIndex(d => new { d.Provider, d.DeliveryId }).IsUnique();
            b.HasIndex(d => d.ReceivedAtUtc);

            b.HasOne(d => d.Repository)
                .WithMany()
                .HasForeignKey(d => d.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Sprint>(b =>
        {
            b.ToTable("sprints");
            b.HasKey(s => s.Id);

            b.Property(s => s.Name).HasMaxLength(120).IsRequired();
            b.Property(s => s.Goal).HasMaxLength(500);
            b.Property(s => s.Status).HasMaxLength(20).IsRequired();

            b.HasIndex(s => new { s.ProjectId, s.Status });
            b.HasIndex(s => s.ProjectId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");

            b.HasOne(s => s.Project)
                .WithMany()
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IssueLink>(b =>
        {
            b.ToTable("issue_links");
            b.HasKey(l => l.Id);

            b.Property(l => l.LinkType).HasMaxLength(30).IsRequired();

            b.HasIndex(l => new { l.SourceTaskId, l.TargetTaskId, l.LinkType }).IsUnique();
            b.HasIndex(l => l.TargetTaskId);

            b.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(l => l.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectBoardColumnConfig>(b =>
        {
            b.ToTable("project_board_column_configs");
            b.HasKey(c => c.Id);

            b.Property(c => c.Status).HasMaxLength(20).IsRequired();
            b.HasIndex(c => new { c.ProjectId, c.Status }).IsUnique();

            b.HasOne(c => c.Project)
                .WithMany()
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SprintBurndownSnapshot>(b =>
        {
            b.ToTable("sprint_burndown_snapshots");
            b.HasKey(s => s.Id);

            b.HasIndex(s => new { s.SprintId, s.SnapshotDate }).IsUnique();

            b.HasOne(s => s.Sprint)
                .WithMany()
                .HasForeignKey(s => s.SprintId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
