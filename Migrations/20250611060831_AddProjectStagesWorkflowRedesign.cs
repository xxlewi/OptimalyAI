using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectStagesWorkflowRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Schedule",
                table: "Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "Projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowVersion",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProjectStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OrchestratorType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrchestratorConfiguration = table.Column<string>(type: "text", nullable: true),
                    ReActAgentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReActAgentConfiguration = table.Column<string>(type: "text", nullable: true),
                    ExecutionStrategy = table.Column<int>(type: "integer", nullable: false),
                    ContinueCondition = table.Column<string>(type: "text", nullable: true),
                    ErrorHandling = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStageTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToolName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: true),
                    InputMapping = table.Column<string>(type: "text", nullable: true),
                    OutputMapping = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutionCondition = table.Column<string>(type: "text", nullable: true),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedOutputFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStageTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStageTools_ProjectStages_ProjectStageId",
                        column: x => x.ProjectStageId,
                        principalTable: "ProjectStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStage_CreatedAt",
                table: "ProjectStages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ProjectId",
                table: "ProjectStages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageTool_CreatedAt",
                table: "ProjectStageTools",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageTools_ProjectStageId",
                table: "ProjectStageTools",
                column: "ProjectStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectStageTools");

            migrationBuilder.DropTable(
                name: "ProjectStages");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Schedule",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "WorkflowVersion",
                table: "Projects");
        }
    }
}
