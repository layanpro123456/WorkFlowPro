using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkFlowPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsAndMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => new { x.ProjectId, x.UserId });
                    table.ForeignKey(
                        name: "FK_project_members_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_ProjectId_ProjectRole",
                table: "project_members",
                columns: new[] { "ProjectId", "ProjectRole" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_UserId",
                table: "project_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_CreatedById",
                table: "projects",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ProjectCode",
                table: "projects",
                column: "ProjectCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_Status",
                table: "projects",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
