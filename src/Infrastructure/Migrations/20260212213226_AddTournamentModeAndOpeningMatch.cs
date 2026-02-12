using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentModeAndOpeningMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpeningMatchId",
                table: "Tournaments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OpeningMatchId",
                table: "Tournaments");
        }
    }
}
