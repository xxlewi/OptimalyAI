using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCounterFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Context = table.Column<string>(type: "text", nullable: true),
                    PreferredModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AllowedTools = table.Column<string>(type: "text", nullable: true),
                    AutoSelectModel = table.Column<bool>(type: "boolean", nullable: false),
                    AutoSelectTools = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectConversations_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectConversations_ProjectStages_ProjectStageId",
                        column: x => x.ProjectStageId,
                        principalTable: "ProjectStages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectConversations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversation_CreatedAt",
                table: "ProjectConversations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversations_ConversationId",
                table: "ProjectConversations",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversations_ProjectId",
                table: "ProjectConversations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversations_ProjectStageId",
                table: "ProjectConversations",
                column: "ProjectStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectConversations");
        }
    }
}
