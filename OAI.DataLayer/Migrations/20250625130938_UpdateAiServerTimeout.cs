using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OAI.DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAiServerTimeout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update timeout for LM Studio server from 30 to 120 seconds
            migrationBuilder.Sql(
                @"UPDATE ""AiServers"" 
                  SET ""TimeoutSeconds"" = 120 
                  WHERE ""ServerType"" = 2 AND ""TimeoutSeconds"" = 30");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert timeout back to 30 seconds
            migrationBuilder.Sql(
                @"UPDATE ""AiServers"" 
                  SET ""TimeoutSeconds"" = 30 
                  WHERE ""ServerType"" = 2 AND ""TimeoutSeconds"" = 120");
        }
    }
}
