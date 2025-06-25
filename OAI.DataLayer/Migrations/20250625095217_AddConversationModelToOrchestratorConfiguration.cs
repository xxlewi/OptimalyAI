using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OAI.DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationModelToOrchestratorConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConversationModelId",
                table: "OrchestratorConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorConfigurations_ConversationModelId",
                table: "OrchestratorConfigurations",
                column: "ConversationModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestratorConfigurations_AiModels_ConversationModelId",
                table: "OrchestratorConfigurations",
                column: "ConversationModelId",
                principalTable: "AiModels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestratorConfigurations_AiModels_ConversationModelId",
                table: "OrchestratorConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestratorConfigurations_ConversationModelId",
                table: "OrchestratorConfigurations");

            migrationBuilder.DropColumn(
                name: "ConversationModelId",
                table: "OrchestratorConfigurations");
        }
    }
}
