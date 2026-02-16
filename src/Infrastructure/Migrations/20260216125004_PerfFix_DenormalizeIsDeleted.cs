using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerfFix_DenormalizeIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TournamentPlayers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TeamRegistrations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TeamJoinRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MatchMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MatchEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Matches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TeamRegistrations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TeamJoinRequests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MatchMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MatchEvents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Matches");
        }
    }
}
