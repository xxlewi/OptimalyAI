using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OAI.DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddWebApplicationEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrchestratorSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrchestratorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsWorkflowNode = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefaultChatOrchestrator = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestratorSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProjectPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProgrammingLanguage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Framework = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Architecture = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Database = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GitRepository = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastDeployment = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorSettings_CreatedAt",
                table: "OrchestratorSettings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebApplication_CreatedAt",
                table: "WebApplications",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrchestratorSettings");

            migrationBuilder.DropTable(
                name: "WebApplications");
        }
    }
}
