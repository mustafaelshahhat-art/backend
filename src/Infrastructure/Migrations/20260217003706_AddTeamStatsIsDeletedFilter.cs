using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamStatsIsDeletedFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamStats_Teams_TeamId",
                table: "TeamStats");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TeamStats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamStats_Teams_TeamId",
                table: "TeamStats",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamStats_Teams_TeamId",
                table: "TeamStats");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TeamStats");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamStats_Teams_TeamId",
                table: "TeamStats",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
