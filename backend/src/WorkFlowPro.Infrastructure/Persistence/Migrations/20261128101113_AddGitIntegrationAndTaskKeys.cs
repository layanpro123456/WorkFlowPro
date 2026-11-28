using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkFlowPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitIntegrationAndTaskKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskKey",
                table: "tasks",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TaskNumber",
                table: "tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "project_git_repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RepositoryOwner = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RepositoryFullName = table.Column<string>(type: "TEXT", maxLength: 210, nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    WebBaseUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "TEXT", nullable: true),
                    EncryptedWebhookSecret = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoLinkEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoTransitionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseInReviewColumn = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_git_repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_git_repositories_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_git_repositories_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_pull_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalPrId = table.Column<long>(type: "INTEGER", nullable: false),
                    PrNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SourceBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TargetBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthorLogin = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorAvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    HtmlUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CheckStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CommitsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Additions = table.Column<int>(type: "INTEGER", nullable: false),
                    Deletions = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MergedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_pull_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_git_pull_requests_project_git_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "project_git_repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DeliveryId = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_webhook_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_git_webhook_deliveries_project_git_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "project_git_repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_git_branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BaseBranch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BranchUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_git_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_git_branches_project_git_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "project_git_repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_git_branches_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_pull_request_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PullRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LinkSource = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_pull_request_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_pull_request_links_git_pull_requests_PullRequestId",
                        column: x => x.PullRequestId,
                        principalTable: "git_pull_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_pull_request_links_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ProjectId_TaskNumber",
                table: "tasks",
                columns: new[] { "ProjectId", "TaskNumber" },
                unique: true,
                filter: "\"TaskNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_TaskKey",
                table: "tasks",
                column: "TaskKey");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorId_OccurredAtUtc",
                table: "audit_logs",
                columns: new[] { "ActorId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OccurredAtUtc",
                table: "audit_logs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_git_pull_requests_RepositoryId_PrNumber",
                table: "git_pull_requests",
                columns: new[] { "RepositoryId", "PrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_git_pull_requests_RepositoryId_SourceBranch",
                table: "git_pull_requests",
                columns: new[] { "RepositoryId", "SourceBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_git_webhook_deliveries_Provider_DeliveryId",
                table: "git_webhook_deliveries",
                columns: new[] { "Provider", "DeliveryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_git_webhook_deliveries_ReceivedAtUtc",
                table: "git_webhook_deliveries",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_git_webhook_deliveries_RepositoryId",
                table: "git_webhook_deliveries",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_project_git_repositories_CreatedById",
                table: "project_git_repositories",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_project_git_repositories_ProjectId",
                table: "project_git_repositories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_git_repositories_ProjectId_Provider_RepositoryFullName",
                table: "project_git_repositories",
                columns: new[] { "ProjectId", "Provider", "RepositoryFullName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_git_branches_RepositoryId_BranchName",
                table: "task_git_branches",
                columns: new[] { "RepositoryId", "BranchName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_git_branches_TaskId",
                table: "task_git_branches",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_task_pull_request_links_PullRequestId",
                table: "task_pull_request_links",
                column: "PullRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_task_pull_request_links_TaskId_PullRequestId",
                table: "task_pull_request_links",
                columns: new[] { "TaskId", "PullRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "git_webhook_deliveries");

            migrationBuilder.DropTable(
                name: "task_git_branches");

            migrationBuilder.DropTable(
                name: "task_pull_request_links");

            migrationBuilder.DropTable(
                name: "git_pull_requests");

            migrationBuilder.DropTable(
                name: "project_git_repositories");

            migrationBuilder.DropIndex(
                name: "IX_tasks_ProjectId_TaskNumber",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_TaskKey",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "TaskKey",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "TaskNumber",
                table: "tasks");
        }
    }
}
