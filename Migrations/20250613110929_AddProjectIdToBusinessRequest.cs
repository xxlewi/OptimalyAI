using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIdToBusinessRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "BusinessRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRequests_ProjectId",
                table: "BusinessRequests",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessRequests_Projects_ProjectId",
                table: "BusinessRequests",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessRequests_Projects_ProjectId",
                table: "BusinessRequests");

            migrationBuilder.DropIndex(
                name: "IX_BusinessRequests_ProjectId",
                table: "BusinessRequests");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "BusinessRequests");
        }
    }
}
