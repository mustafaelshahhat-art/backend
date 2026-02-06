using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentWinner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WinnerTeamId",
                table: "Tournaments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_WinnerTeamId",
                table: "Tournaments",
                column: "WinnerTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Teams_WinnerTeamId",
                table: "Tournaments",
                column: "WinnerTeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Teams_WinnerTeamId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_WinnerTeamId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "WinnerTeamId",
                table: "Tournaments");
        }
    }
}
