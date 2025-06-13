using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class FixBusinessRequestProjectRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessRequests_Projects_ProjectId1",
                table: "BusinessRequests");

            migrationBuilder.DropIndex(
                name: "IX_BusinessRequests_ProjectId1",
                table: "BusinessRequests");

            migrationBuilder.DropColumn(
                name: "ProjectId1",
                table: "BusinessRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId1",
                table: "BusinessRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRequests_ProjectId1",
                table: "BusinessRequests",
                column: "ProjectId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessRequests_Projects_ProjectId1",
                table: "BusinessRequests",
                column: "ProjectId1",
                principalTable: "Projects",
                principalColumn: "Id");
        }
    }
}
