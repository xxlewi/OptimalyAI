using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimalyAI.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ICO = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DIC = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Mobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillingStreet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillingZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BillingCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveryStreet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveryZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeliveryCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Segment = table.Column<int>(type: "integer", nullable: false),
                    PreferredCommunication = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    FirstContactDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastContactDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalProjectsValue = table.Column<decimal>(type: "numeric", nullable: false),
                    ProjectsCount = table.Column<int>(type: "integer", nullable: false),
                    AverageProjectSuccessRate = table.Column<decimal>(type: "numeric", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric", nullable: true),
                    CurrentDebt = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentTermDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Mobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PreferredContactTime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomerRequirement = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProjectType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric", nullable: true),
                    ActualHours = table.Column<decimal>(type: "numeric", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric", nullable: true),
                    Configuration = table.Column<string>(type: "text", nullable: true),
                    ProjectContext = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CustomerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EstimatedBudget = table.Column<decimal>(type: "numeric", nullable: true),
                    RequestedDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Resolution = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    Attachments = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHistorys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProjectVersion = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHistorys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHistorys_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MetricName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: false),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    BillingRate = table.Column<decimal>(type: "numeric", nullable: true),
                    BillingAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMetrics_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectOrchestrators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrchestratorType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrchestratorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectOrchestrators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectOrchestrators_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToolName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                    DefaultParameters = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDailyUsage = table.Column<int>(type: "integer", nullable: true),
                    TodayUsageCount = table.Column<int>(type: "integer", nullable: false),
                    TotalUsageCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AverageExecutionTime = table.Column<double>(type: "double precision", nullable: true),
                    SuccessRate = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTools_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WorkflowType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StepsDefinition = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    LastExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    AverageExecutionTime = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectWorkflows_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Importance = table.Column<int>(type: "integer", nullable: false),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RelatedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNotes_CustomerRequests_RelatedRequestId",
                        column: x => x.RelatedRequestId,
                        principalTable: "CustomerRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CustomerNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerNotes_Projects_RelatedProjectId",
                        column: x => x.RelatedProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    InputParameters = table.Column<string>(type: "text", nullable: false),
                    OutputData = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: false),
                    ErrorStackTrace = table.Column<string>(type: "text", nullable: false),
                    ToolsUsedCount = table.Column<int>(type: "integer", nullable: false),
                    ItemsProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    ExecutionCost = table.Column<decimal>(type: "numeric", nullable: true),
                    ExecutionLog = table.Column<string>(type: "text", nullable: false),
                    InitiatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectExecutions_ProjectWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "ProjectWorkflows",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectExecutions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContact_CreatedAt",
                table: "CustomerContacts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_CustomerId",
                table: "CustomerContacts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNote_CreatedAt",
                table: "CustomerNotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_CustomerId",
                table: "CustomerNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_RelatedProjectId",
                table: "CustomerNotes",
                column: "RelatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_RelatedRequestId",
                table: "CustomerNotes",
                column: "RelatedRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequest_CreatedAt",
                table: "CustomerRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_CustomerId",
                table: "CustomerRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_ProjectId",
                table: "CustomerRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_CreatedAt",
                table: "Customers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExecution_CreatedAt",
                table: "ProjectExecutions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExecutions_ProjectId",
                table: "ProjectExecutions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExecutions_WorkflowId",
                table: "ProjectExecutions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFile_CreatedAt",
                table: "ProjectFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_ProjectId",
                table: "ProjectFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHistory_CreatedAt",
                table: "ProjectHistorys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHistorys_ProjectId",
                table: "ProjectHistorys",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMetric_CreatedAt",
                table: "ProjectMetrics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMetrics_ProjectId",
                table: "ProjectMetrics",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOrchestrator_CreatedAt",
                table: "ProjectOrchestrators",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOrchestrators_ProjectId",
                table: "ProjectOrchestrators",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_CreatedAt",
                table: "Projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CustomerId",
                table: "Projects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTool_CreatedAt",
                table: "ProjectTools",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTools_ProjectId",
                table: "ProjectTools",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectWorkflow_CreatedAt",
                table: "ProjectWorkflows",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectWorkflows_ProjectId",
                table: "ProjectWorkflows",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerContacts");

            migrationBuilder.DropTable(
                name: "CustomerNotes");

            migrationBuilder.DropTable(
                name: "ProjectExecutions");

            migrationBuilder.DropTable(
                name: "ProjectFiles");

            migrationBuilder.DropTable(
                name: "ProjectHistorys");

            migrationBuilder.DropTable(
                name: "ProjectMetrics");

            migrationBuilder.DropTable(
                name: "ProjectOrchestrators");

            migrationBuilder.DropTable(
                name: "ProjectTools");

            migrationBuilder.DropTable(
                name: "CustomerRequests");

            migrationBuilder.DropTable(
                name: "ProjectWorkflows");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
