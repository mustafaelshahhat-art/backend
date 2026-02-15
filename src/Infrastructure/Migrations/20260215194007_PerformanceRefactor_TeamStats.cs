using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceRefactor_TeamStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Already applied in DB but missing from EF History
            /*
            migrationBuilder.DropColumn(
                name: "QualifiedTeamsPerGroup",
                table: "Tournaments");

            migrationBuilder.RenameColumn(
                name: "SeedingMode",
                table: "Tournaments",
                newName: "SchedulingMode");

            migrationBuilder.RenameColumn(
                name: "OpeningMatchHomeTeamId",
                table: "Tournaments",
                newName: "OpeningTeamBId");

            migrationBuilder.RenameColumn(
                name: "OpeningMatchAwayTeamId",
                table: "Tournaments",
                newName: "OpeningTeamAId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Tournaments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "TeamRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpeningMatch",
                table: "Matches",
                type: "bit",
                nullable: false,
                defaultValue: false);
            */

            migrationBuilder.CreateTable(
                name: "TeamStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    Draws = table.Column<int>(type: "int", nullable: false),
                    GoalsFor = table.Column<int>(type: "int", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "int", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            /*
            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatedAt",
                table: "Tournaments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_StartDate",
                table: "Tournaments",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "UQ_Tournaments_Name",
                table: "Tournaments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamRegistration_CreatedAt",
                table: "TeamRegistrations",
                column: "CreatedAt");
            */

            migrationBuilder.CreateIndex(
                name: "IX_TeamStats_TeamId",
                table: "TeamStats",
                column: "TeamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamStats");

            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_CreatedAt",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_StartDate",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "UQ_Tournaments_Name",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_TeamRegistration_CreatedAt",
                table: "TeamRegistrations");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "TeamRegistrations");

            migrationBuilder.DropColumn(
                name: "IsOpeningMatch",
                table: "Matches");

            migrationBuilder.RenameColumn(
                name: "SchedulingMode",
                table: "Tournaments",
                newName: "SeedingMode");

            migrationBuilder.RenameColumn(
                name: "OpeningTeamBId",
                table: "Tournaments",
                newName: "OpeningMatchHomeTeamId");

            migrationBuilder.RenameColumn(
                name: "OpeningTeamAId",
                table: "Tournaments",
                newName: "OpeningMatchAwayTeamId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Tournaments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "QualifiedTeamsPerGroup",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
