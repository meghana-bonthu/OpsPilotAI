using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsPilot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsAndIncidentAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Incidents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TeamId",
                table: "Incidents",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Teams_TeamId",
                table: "Incidents",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Teams_TeamId",
                table: "Incidents");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_TeamId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Incidents");
        }
    }
}
