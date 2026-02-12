using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRefereeRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_RefereeId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_RefereeId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RefereeId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RefereeNotes",
                table: "Matches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RefereeId",
                table: "Matches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefereeNotes",
                table: "Matches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_RefereeId",
                table: "Matches",
                column: "RefereeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_RefereeId",
                table: "Matches",
                column: "RefereeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
