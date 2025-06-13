using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class RenameBusinessRequestToRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_BusinessRequests_BusinessRequestId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestExecutions_BusinessRequests_BusinessRequestId",
                table: "RequestExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestFiles_BusinessRequests_BusinessRequestId",
                table: "RequestFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestNotes_BusinessRequests_BusinessRequestId",
                table: "RequestNotes");

            migrationBuilder.DropTable(
                name: "BusinessRequests");

            migrationBuilder.RenameColumn(
                name: "BusinessRequestId",
                table: "RequestNotes",
                newName: "RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestNotes_BusinessRequestId",
                table: "RequestNotes",
                newName: "IX_RequestNotes_RequestId");

            migrationBuilder.RenameColumn(
                name: "BusinessRequestId",
                table: "RequestFiles",
                newName: "RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestFiles_BusinessRequestId",
                table: "RequestFiles",
                newName: "IX_RequestFiles_RequestId");

            migrationBuilder.RenameColumn(
                name: "BusinessRequestId",
                table: "RequestExecutions",
                newName: "RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestExecutions_BusinessRequestId",
                table: "RequestExecutions",
                newName: "IX_RequestExecutions_RequestId");

            migrationBuilder.RenameColumn(
                name: "BusinessRequestId",
                table: "Conversations",
                newName: "RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_BusinessRequestId",
                table: "Conversations",
                newName: "IX_Conversations_RequestId");

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowTemplateId = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Requests_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Request_CreatedAt",
                table: "Requests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ProjectId",
                table: "Requests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequestNumber",
                table: "Requests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_WorkflowTemplateId",
                table: "Requests",
                column: "WorkflowTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Requests_RequestId",
                table: "Conversations",
                column: "RequestId",
                principalTable: "Requests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestExecutions_Requests_RequestId",
                table: "RequestExecutions",
                column: "RequestId",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestFiles_Requests_RequestId",
                table: "RequestFiles",
                column: "RequestId",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestNotes_Requests_RequestId",
                table: "RequestNotes",
                column: "RequestId",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Requests_RequestId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestExecutions_Requests_RequestId",
                table: "RequestExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestFiles_Requests_RequestId",
                table: "RequestFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestNotes_Requests_RequestId",
                table: "RequestNotes");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "RequestNotes",
                newName: "BusinessRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestNotes_RequestId",
                table: "RequestNotes",
                newName: "IX_RequestNotes_BusinessRequestId");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "RequestFiles",
                newName: "BusinessRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestFiles_RequestId",
                table: "RequestFiles",
                newName: "IX_RequestFiles_BusinessRequestId");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "RequestExecutions",
                newName: "BusinessRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_RequestExecutions_RequestId",
                table: "RequestExecutions",
                newName: "IX_RequestExecutions_BusinessRequestId");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "Conversations",
                newName: "BusinessRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_RequestId",
                table: "Conversations",
                newName: "IX_Conversations_BusinessRequestId");

            migrationBuilder.CreateTable(
                name: "BusinessRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowTemplateId = table.Column<int>(type: "integer", nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric", nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequestNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BusinessRequests_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRequest_CreatedAt",
                table: "BusinessRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRequests_ProjectId",
                table: "BusinessRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRequests_RequestNumber",
                table: "BusinessRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRequests_WorkflowTemplateId",
                table: "BusinessRequests",
                column: "WorkflowTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_BusinessRequests_BusinessRequestId",
                table: "Conversations",
                column: "BusinessRequestId",
                principalTable: "BusinessRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestExecutions_BusinessRequests_BusinessRequestId",
                table: "RequestExecutions",
                column: "BusinessRequestId",
                principalTable: "BusinessRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestFiles_BusinessRequests_BusinessRequestId",
                table: "RequestFiles",
                column: "BusinessRequestId",
                principalTable: "BusinessRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestNotes_BusinessRequests_BusinessRequestId",
                table: "RequestNotes",
                column: "BusinessRequestId",
                principalTable: "BusinessRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
