using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReporterOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReporterUserId",
                table: "Incidents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReporterUserId",
                table: "Incidents");
        }
    }
}
