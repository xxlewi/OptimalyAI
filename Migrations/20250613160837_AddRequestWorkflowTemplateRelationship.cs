using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestWorkflowTemplateRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId",
                table: "Requests");

            migrationBuilder.AddColumn<int>(
                name: "WorkflowTemplateId1",
                table: "Requests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_WorkflowTemplateId1",
                table: "Requests",
                column: "WorkflowTemplateId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId",
                table: "Requests",
                column: "WorkflowTemplateId",
                principalTable: "WorkflowTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId1",
                table: "Requests",
                column: "WorkflowTemplateId1",
                principalTable: "WorkflowTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId",
                table: "Requests");

            migrationBuilder.DropForeignKey(
                name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId1",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_WorkflowTemplateId1",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "WorkflowTemplateId1",
                table: "Requests");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId",
                table: "Requests",
                column: "WorkflowTemplateId",
                principalTable: "WorkflowTemplates",
                principalColumn: "Id");
        }
    }
}
