using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentSuggestedActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentSuggestedActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentSuggestedActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentSuggestedActions_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentSuggestedActions_IncidentId_CreatedAtUtc",
                table: "IncidentSuggestedActions",
                columns: new[] { "IncidentId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentSuggestedActions");
        }
    }
}
