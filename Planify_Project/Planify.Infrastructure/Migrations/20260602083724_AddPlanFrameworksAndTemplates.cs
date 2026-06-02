using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanFrameworksAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReminderSent",
                table: "PlanTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DraftExpiresAt",
                table: "Plans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReminderSent",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlanFrameworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Structure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFrameworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFrameworks_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanTemplates_PlanFrameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "PlanFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanTemplates_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_FrameworkId",
                table: "Plans",
                column: "FrameworkId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_TemplateId",
                table: "Plans",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFrameworks_CreatedBy",
                table: "PlanFrameworks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplates_CreatedBy",
                table: "PlanTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplates_FrameworkId",
                table: "PlanTemplates",
                column: "FrameworkId");

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_PlanFrameworks_FrameworkId",
                table: "Plans",
                column: "FrameworkId",
                principalTable: "PlanFrameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_PlanTemplates_TemplateId",
                table: "Plans",
                column: "TemplateId",
                principalTable: "PlanTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plans_PlanFrameworks_FrameworkId",
                table: "Plans");

            migrationBuilder.DropForeignKey(
                name: "FK_Plans_PlanTemplates_TemplateId",
                table: "Plans");

            migrationBuilder.DropTable(
                name: "PlanTemplates");

            migrationBuilder.DropTable(
                name: "PlanFrameworks");

            migrationBuilder.DropIndex(
                name: "IX_Plans_FrameworkId",
                table: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Plans_TemplateId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "IsReminderSent",
                table: "PlanTasks");

            migrationBuilder.DropColumn(
                name: "DraftExpiresAt",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "IsReminderSent",
                table: "Plans");
        }
    }
}
