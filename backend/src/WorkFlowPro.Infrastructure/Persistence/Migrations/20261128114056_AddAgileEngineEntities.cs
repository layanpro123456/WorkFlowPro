using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkFlowPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgileEngineEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoardRank",
                table: "tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "EpicId",
                table: "tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssueType",
                table: "tasks",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Task");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTaskId",
                table: "tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SprintId",
                table: "tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoryPoints",
                table: "tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "issue_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LinkType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_issue_links_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issue_links_tasks_SourceTaskId",
                        column: x => x.SourceTaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_issue_links_tasks_TargetTaskId",
                        column: x => x.TargetTaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_board_column_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    WipLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    IsStrictWip = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_board_column_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_board_column_configs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Goal = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprints_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sprints_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprint_burndown_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SprintId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingTaskCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_burndown_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprint_burndown_snapshots_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_EpicId",
                table: "tasks",
                column: "EpicId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ParentTaskId",
                table: "tasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ProjectId_IssueType",
                table: "tasks",
                columns: new[] { "ProjectId", "IssueType" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ProjectId_SprintId_Status_BoardRank",
                table: "tasks",
                columns: new[] { "ProjectId", "SprintId", "Status", "BoardRank" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_SprintId",
                table: "tasks",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_CreatedById",
                table: "issue_links",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_SourceTaskId_TargetTaskId_LinkType",
                table: "issue_links",
                columns: new[] { "SourceTaskId", "TargetTaskId", "LinkType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_TargetTaskId",
                table: "issue_links",
                column: "TargetTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_project_board_column_configs_ProjectId_Status",
                table: "project_board_column_configs",
                columns: new[] { "ProjectId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sprint_burndown_snapshots_SprintId_SnapshotDate",
                table: "sprint_burndown_snapshots",
                columns: new[] { "SprintId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sprints_CreatedById",
                table: "sprints",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_sprints_ProjectId",
                table: "sprints",
                column: "ProjectId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_sprints_ProjectId_Status",
                table: "sprints",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_sprints_SprintId",
                table: "tasks",
                column: "SprintId",
                principalTable: "sprints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_tasks_EpicId",
                table: "tasks",
                column: "EpicId",
                principalTable: "tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_tasks_ParentTaskId",
                table: "tasks",
                column: "ParentTaskId",
                principalTable: "tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_sprints_SprintId",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_tasks_EpicId",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_tasks_ParentTaskId",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "issue_links");

            migrationBuilder.DropTable(
                name: "project_board_column_configs");

            migrationBuilder.DropTable(
                name: "sprint_burndown_snapshots");

            migrationBuilder.DropTable(
                name: "sprints");

            migrationBuilder.DropIndex(
                name: "IX_tasks_EpicId",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_ParentTaskId",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_ProjectId_IssueType",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_ProjectId_SprintId_Status_BoardRank",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_SprintId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "BoardRank",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "EpicId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "IssueType",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "SprintId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "StoryPoints",
                table: "tasks");
        }
    }
}
