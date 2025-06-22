using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OAI.DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrchestratorConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrchestratorConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrchestratorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    AiServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultModelId1 = table.Column<int>(type: "integer", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestratorConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrchestratorConfigurations_AiModels_DefaultModelId1",
                        column: x => x.DefaultModelId1,
                        principalTable: "AiModels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrchestratorConfigurations_AiServers_AiServerId",
                        column: x => x.AiServerId,
                        principalTable: "AiServers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorConfiguration_CreatedAt",
                table: "OrchestratorConfigurations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorConfigurations_AiServerId",
                table: "OrchestratorConfigurations",
                column: "AiServerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorConfigurations_DefaultModelId1",
                table: "OrchestratorConfigurations",
                column: "DefaultModelId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrchestratorConfigurations");
        }
    }
}
