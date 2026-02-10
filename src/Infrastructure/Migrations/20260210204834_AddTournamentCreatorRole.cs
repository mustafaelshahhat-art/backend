using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentCreatorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatorUserId",
                table: "Tournaments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatorUserId",
                table: "Tournaments",
                column: "CreatorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Users_CreatorUserId",
                table: "Tournaments",
                column: "CreatorUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Users_CreatorUserId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_CreatorUserId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "CreatorUserId",
                table: "Tournaments");
        }
    }
}
