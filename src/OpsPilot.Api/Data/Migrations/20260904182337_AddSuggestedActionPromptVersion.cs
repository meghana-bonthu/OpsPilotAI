using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestedActionPromptVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "IncidentSuggestedActions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "incident-suggested-action-v1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "IncidentSuggestedActions");
        }
    }
}
