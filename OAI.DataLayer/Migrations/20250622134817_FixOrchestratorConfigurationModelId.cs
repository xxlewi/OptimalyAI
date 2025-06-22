using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OAI.DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class FixOrchestratorConfigurationModelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestratorConfigurations_AiModels_DefaultModelId1",
                table: "OrchestratorConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestratorConfigurations_DefaultModelId1",
                table: "OrchestratorConfigurations");

            migrationBuilder.DropColumn(
                name: "DefaultModelId1",
                table: "OrchestratorConfigurations");

            migrationBuilder.AlterColumn<int>(
                name: "DefaultModelId",
                table: "OrchestratorConfigurations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorConfigurations_DefaultModelId",
                table: "OrchestratorConfigurations",
                column: "DefaultModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestratorConfigurations_AiModels_DefaultModelId",
                table: "OrchestratorConfigurations",
                column: "DefaultModelId",
                principalTable: "AiModels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestratorConfigurations_AiModels_DefaultModelId",
                table: "OrchestratorConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_OrchestratorConfigurations_DefaultModelId",
                table: "OrchestratorConfigurations");

            migrationBuilder.AlterColumn<Guid>(
                name: "DefaultModelId",
                table: "OrchestratorConfigurations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultModelId1",
                table: "OrchestratorConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestratorConfigurations_DefaultModelId1",
                table: "OrchestratorConfigurations",
                column: "DefaultModelId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestratorConfigurations_AiModels_DefaultModelId1",
                table: "OrchestratorConfigurations",
                column: "DefaultModelId1",
                principalTable: "AiModels",
                principalColumn: "Id");
        }
    }
}
