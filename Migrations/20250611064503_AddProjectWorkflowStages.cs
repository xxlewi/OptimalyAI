using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectWorkflowStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectStageTools_ProjectStageId",
                table: "ProjectStageTools");

            migrationBuilder.DropIndex(
                name: "IX_ProjectStages_ProjectId",
                table: "ProjectStages");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageTools_ProjectStageId_Order",
                table: "ProjectStageTools",
                columns: new[] { "ProjectStageId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ProjectId_Order",
                table: "ProjectStages",
                columns: new[] { "ProjectId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectStageTools_ProjectStageId_Order",
                table: "ProjectStageTools");

            migrationBuilder.DropIndex(
                name: "IX_ProjectStages_ProjectId_Order",
                table: "ProjectStages");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageTools_ProjectStageId",
                table: "ProjectStageTools",
                column: "ProjectStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ProjectId",
                table: "ProjectStages",
                column: "ProjectId");
        }
    }
}
