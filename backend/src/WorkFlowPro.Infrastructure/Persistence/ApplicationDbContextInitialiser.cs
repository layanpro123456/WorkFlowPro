using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkFlowPro.Domain.Constants;
using WorkFlowPro.Domain.Entities;
using WorkFlowPro.Infrastructure.Identity;

namespace WorkFlowPro.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsNpgsql())
            {
                await _context.Database.MigrateAsync();
            }
            else
            {
                await _context.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // 1. Seed Roles
        foreach (var roleName in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }

        // 2. Seed default development admin user
        const string adminEmail = "admin@workflowpro.local";
        var adminUser = await _userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "System Administrator",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(adminUser, "Admin@123456");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, Roles.Administrator);
                _logger.LogInformation("Seeded default administrator account {AdminEmail}", adminEmail);
            }
            else
            {
                _logger.LogWarning("Failed to seed admin user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }

        // 2b. Seed inactive system automation actor for Git/PR status transitions
        const string automationEmail = "git-automation@workflowpro.local";
        if (await _userManager.FindByEmailAsync(automationEmail) == null)
        {
            var botUser = new ApplicationUser
            {
                UserName = automationEmail,
                Email = automationEmail,
                DisplayName = "Git Automation",
                IsActive = false,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _userManager.CreateAsync(botUser, $"GitBot!{Guid.NewGuid():N}");
        }

        // 2c. Backfill sequential TaskNumber / TaskKey for any legacy tasks
        var unkeyedTasks = await _context.Tasks
            .Include(t => t.Project)
            .Where(t => t.TaskNumber == 0 && t.Project != null)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync();

        if (unkeyedTasks.Count > 0)
        {
            var maxByProject = await _context.Tasks
                .Where(t => t.TaskNumber > 0)
                .GroupBy(t => t.ProjectId)
                .Select(g => new { ProjectId = g.Key, MaxNumber = g.Max(t => t.TaskNumber) })
                .ToDictionaryAsync(x => x.ProjectId, x => x.MaxNumber);

            foreach (var task in unkeyedTasks)
            {
                var next = maxByProject.GetValueOrDefault(task.ProjectId, 0) + 1;
                maxByProject[task.ProjectId] = next;
                task.AssignTaskKey(next, task.Project!.ProjectCode);
            }
            await _context.SaveChangesAsync();
        }

        // 3. Seed synthetic demo data if explicitly configured (e.g. Docker demo deployment)
        var shouldSeedDemo = _configuration.GetValue<bool>("SeedDemoData", false);
        if (shouldSeedDemo && adminUser != null)
        {
            await SeedDemoDataAsync(adminUser);
        }
    }

    private async Task SeedDemoDataAsync(ApplicationUser adminUser)
    {
        // Seed Demo Users
        var manager = await EnsureDemoUserAsync("manager@workflowpro.local", "Alex Mercer (Project Lead)", Roles.Manager);
        var agent = await EnsureDemoUserAsync("agent@workflowpro.local", "Sarah Connor (Support Lead)", Roles.SupportAgent);
        var employee1 = await EnsureDemoUserAsync("employee@workflowpro.local", "David Chen (Software Engineer)", Roles.Employee);
        var employee2 = await EnsureDemoUserAsync("employee2@workflowpro.local", "Elena Rostova (UI Designer)", Roles.Employee);

        if (manager == null || agent == null || employee1 == null || employee2 == null)
        {
            return;
        }

        // Seed Demo Projects
        var prjAlpha = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "PRJ-ALPHA");
        if (prjAlpha == null)
        {
            prjAlpha = new Project("PRJ-ALPHA", "Cloud Migration Platform", "Enterprise workload containerization and zero-downtime database migration.", adminUser.Id);
            prjAlpha.AddMember(manager.Id, ProjectRoles.Manager);
            prjAlpha.AddMember(employee1.Id, ProjectRoles.Member);
            prjAlpha.AddMember(employee2.Id, ProjectRoles.Member);
            _context.Projects.Add(prjAlpha);
            await _context.SaveChangesAsync();
        }

        var prjBeta = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "PRJ-BETA");
        if (prjBeta == null)
        {
            prjBeta = new Project("PRJ-BETA", "Customer Portal & Mobile", "Modern Angular web application and cross-platform mobile app.", manager.Id);
            prjBeta.AddMember(employee1.Id, ProjectRoles.Member);
            prjBeta.AddMember(employee2.Id, ProjectRoles.Member);
            _context.Projects.Add(prjBeta);
            await _context.SaveChangesAsync();
        }

        // Seed Demo Tasks, Sprints, Epics & Subtasks
        if (!await _context.Tasks.AnyAsync(t => t.ProjectId == prjAlpha.Id))
        {
            var closedSprint = new Sprint(prjAlpha.Id, "Sprint 1 — Foundation", manager.Id, "Core cloud & DB bootstrap");
            closedSprint.Start(DateTime.UtcNow.AddDays(-21), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(-21));
            closedSprint.Complete(DateTime.UtcNow.AddDays(-7));

            var activeSprint = new Sprint(prjAlpha.Id, "Sprint 2 — Cloud Cutover", manager.Id, "RDS cutover, JWT rotation & security hardening");
            activeSprint.Start(DateTime.UtcNow.AddDays(-6), DateTime.UtcNow.AddDays(8), DateTime.UtcNow.AddDays(-6));

            _context.Sprints.AddRange(closedSprint, activeSprint);

            var epic = new TaskItem(prjAlpha.Id, "Enterprise Cloud Modernization", "Top-level migration epic", manager.Id, TaskPriorities.High, manager.Id, null, null, TaskStatuses.InProgress, IssueTypes.Epic, 21, null, null, null, 10);
            epic.AssignTaskKey(1, prjAlpha.ProjectCode);

            var task1 = new TaskItem(prjAlpha.Id, "Setup Terraform infrastructure", "Initial cloud infrastructure", adminUser.Id, TaskPriorities.High, employee1.Id, DateTime.UtcNow.AddDays(5), 180, TaskStatuses.Done, IssueTypes.Story, 5, epic.Id, null, closedSprint.Id, 100);
            task1.AssignTaskKey(2, prjAlpha.ProjectCode);
            task1.AddComment(adminUser.Id, "Terraform cloud state bucket is configured.");

            var task2 = new TaskItem(prjAlpha.Id, "Migrate PostgreSQL database to RDS", "Zero-downtime replication setup", manager.Id, TaskPriorities.Urgent, employee1.Id, DateTime.UtcNow.AddDays(2), 240, TaskStatuses.InProgress, IssueTypes.Story, 8, epic.Id, null, activeSprint.Id, 100);
            task2.AssignTaskKey(3, prjAlpha.ProjectCode);
            task2.AddComment(employee1.Id, "Executing pg_dump snapshot right now.");

            var task3 = new TaskItem(prjAlpha.Id, "Implement JWT rotation in API gateway", "Token rotation security", manager.Id, TaskPriorities.High, employee2.Id, DateTime.UtcNow.AddDays(7), 120, TaskStatuses.ToDo, IssueTypes.Task, 3, epic.Id, null, activeSprint.Id, 200);
            task3.AssignTaskKey(4, prjAlpha.ProjectCode);

            var task4 = new TaskItem(prjAlpha.Id, "Audit container security vulnerabilities", "Security scanning", adminUser.Id, TaskPriorities.Urgent, employee1.Id, DateTime.UtcNow.AddDays(-1), 90, TaskStatuses.Blocked, IssueTypes.Bug, 5, epic.Id, null, activeSprint.Id, 300);
            task4.AssignTaskKey(5, prjAlpha.ProjectCode);
            task4.AddComment(employee1.Id, "Awaiting approval for base image upgrade.");

            var subtask1 = new TaskItem(prjAlpha.Id, "Verify RDS read-replica lag metrics", "Subtask for RDS cutover", employee1.Id, TaskPriorities.Normal, employee1.Id, null, 45, TaskStatuses.Done, IssueTypes.Subtask, 2, epic.Id, task2.Id, activeSprint.Id, 150);
            subtask1.AssignTaskKey(6, prjAlpha.ProjectCode);

            _context.Tasks.AddRange(epic, task1, task2, task3, task4, subtask1);
            _context.SprintBurndownSnapshots.Add(new SprintBurndownSnapshot(closedSprint.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7).Date), 5, 0, 5, 0, DateTime.UtcNow.AddDays(-7)));
            _context.SprintBurndownSnapshots.Add(new SprintBurndownSnapshot(activeSprint.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6).Date), 18, 18, 0, 4, DateTime.UtcNow.AddDays(-6)));
            _context.SprintBurndownSnapshots.Add(new SprintBurndownSnapshot(activeSprint.Id, DateOnly.FromDateTime(DateTime.UtcNow.Date), 18, 16, 2, 3, DateTime.UtcNow));

            await _context.SaveChangesAsync();

            // Seed Demo Git Repository & Linked Branch/PR
            if (!await _context.ProjectGitRepositories.AnyAsync(r => r.ProjectId == prjAlpha.Id))
            {
                var demoRepo = new ProjectGitRepository(
                    prjAlpha.Id,
                    GitProviders.GitHub,
                    "layanpro123456",
                    "WorkFlowPro",
                    "main",
                    manager.Id);
                _context.ProjectGitRepositories.Add(demoRepo);

                var demoBranch = new TaskGitBranch(
                    task2.Id,
                    demoRepo.Id,
                    "feat/PRJ-ALPHA-3-rds-replication",
                    "main",
                    demoRepo.BuildBranchWebUrl("feat/PRJ-ALPHA-3-rds-replication"),
                    employee1.Id);
                _context.TaskGitBranches.Add(demoBranch);

                var demoPr = new GitPullRequest(
                    repositoryId: demoRepo.Id,
                    externalPrId: 10042,
                    prNumber: 42,
                    title: "feat(PRJ-ALPHA-3): Zero-downtime PostgreSQL RDS replication",
                    body: "Implements read-replica cutover scripts for PRJ-ALPHA-3.",
                    state: PullRequestStates.Open,
                    sourceBranch: "feat/PRJ-ALPHA-3-rds-replication",
                    targetBranch: "main",
                    authorLogin: "davidchen",
                    htmlUrl: "https://github.com/layanpro123456/WorkFlowPro/pull/42",
                    openedAtUtc: DateTime.UtcNow.AddHours(-6),
                    reviewStatus: PullRequestReviewStatuses.Approved,
                    checkStatus: PullRequestCheckStatuses.Success,
                    commitsCount: 4,
                    additions: 184,
                    deletions: 27);
                _context.GitPullRequests.Add(demoPr);

                _context.TaskPullRequestLinks.Add(new TaskPullRequestLink(task2.Id, demoPr.Id, TaskPullRequestLinkSources.AutoBranch));
                await _context.SaveChangesAsync();
            }
        }

        // Seed Demo Tickets
        if (!await _context.SupportTickets.AnyAsync())
        {
            var ticket1 = new SupportTicket(employee1.Id, "VPN connection dropping intermittently", "Every 30 minutes the European VPN resets.", TicketCategories.Technical, TicketPriorities.High, prjAlpha.Id);
            ticket1.AssignTicketNumber(1001);
            ticket1.Assign(agent.Id, adminUser.Id, "Assigned to support agent.");
            ticket1.AddMessage(employee1.Id, "Every 30 minutes the connection resets.", false);
            ticket1.AddMessage(agent.Id, "Checking gateway keep-alive configuration.", true);

            var ticket2 = new SupportTicket(employee2.Id, "Access request for AWS production logs", "Need read access to CloudWatch logs.", TicketCategories.Access, TicketPriorities.Normal, prjBeta.Id);
            ticket2.AssignTicketNumber(1002);
            ticket2.Assign(agent.Id, adminUser.Id, "Assigned for access approval.");

            _context.SupportTickets.AddRange(ticket1, ticket2);
            await _context.SaveChangesAsync();
        }

        // Seed Demo Approval Workflow & Request
        var workflow = await _context.WorkflowDefinitions.Include(w => w.Steps).FirstOrDefaultAsync(w => w.Name == "Capital Expenditure Approval");
        if (workflow == null)
        {
            workflow = new WorkflowDefinition("Capital Expenditure Approval", "Approval hierarchy for purchases over $1,000", adminUser.Id);
            workflow.SetSteps(new[]
            {
                new WorkflowStep(workflow.Id, 1, "Engineering Lead Review", Roles.Manager, null),
                new WorkflowStep(workflow.Id, 2, "Executive Signoff", Roles.Administrator, null)
            });
            _context.WorkflowDefinitions.Add(workflow);
            await _context.SaveChangesAsync();
        }

        if (!await _context.ApprovalRequests.AnyAsync())
        {
            var approvalReq = new ApprovalRequest(workflow.Id, employee1.Id, "High-spec Developer Workstation Purchase", "Dual 4K monitors and 64GB RAM workstation for local AI development.");
            approvalReq.AssignRequestNumber(1001);
            approvalReq.AddStepSnapshot(1, "Engineering Lead Review", manager.Id);
            approvalReq.AddStepSnapshot(2, "Executive Signoff", adminUser.Id);

            _context.ApprovalRequests.Add(approvalReq);
            await _context.SaveChangesAsync();

            // Seed Notifications for Demo Users
            var notif1 = new InAppNotification(manager.Id, NotificationTypes.ApprovalActionRequired, "Action Required: Approval #1001", "Approval Request #1001 ('High-spec Developer Workstation') requires your review.", "Approval", approvalReq.Id);
            var notif2 = new InAppNotification(employee1.Id, NotificationTypes.TaskAssigned, "Task Assigned: PRJ-ALPHA", "You were assigned to task 'Migrate PostgreSQL database to RDS'.", "Task", Guid.NewGuid());
            _context.Notifications.AddRange(notif1, notif2);
            await _context.SaveChangesAsync();
        }
    }

    private async Task<ApplicationUser?> EnsureDemoUserAsync(string email, string displayName, string role)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var res = await _userManager.CreateAsync(user, "Password@123");
            if (res.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                _logger.LogInformation("Seeded demo user {Email} ({Role})", email, role);
            }
        }
        return user;
    }
}
