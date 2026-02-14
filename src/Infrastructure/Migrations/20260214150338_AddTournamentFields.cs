using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Tournaments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinTeams",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Tournaments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Tournaments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "MinTeams",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Tournaments");
        }
    }
}
