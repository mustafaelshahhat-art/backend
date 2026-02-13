using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Perform manual data conversion for string statuses to integers
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '0' WHERE Status = 'Draft' OR Status = 'draft'");
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '1' WHERE Status = 'RegistrationOpen' OR Status = 'registration_open'");
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '2' WHERE Status = 'RegistrationClosed' OR Status = 'registration_closed'");
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '3' WHERE Status = 'Active' OR Status = 'active'");
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '4' WHERE Status = 'WaitingForOpeningMatchSelection'");
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '5' WHERE Status = 'Completed' OR Status = 'completed'");
            migrationBuilder.Sql("UPDATE Tournaments SET Status = '6' WHERE Status = 'Cancelled' OR Status = 'cancelled'");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Tournaments",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<bool>(
                name: "AllowLateRegistration",
                table: "Tournaments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LateRegistrationMode",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OpeningMatchAwayTeamId",
                table: "Tournaments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpeningMatchHomeTeamId",
                table: "Tournaments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TournamentPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentPlayers_TeamRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "TeamRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentPlayers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentPlayers_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_PlayerId",
                table: "TournamentPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_RegistrationId",
                table: "TournamentPlayers",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TeamId",
                table: "TournamentPlayers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "UQ_TournamentPlayer_Tournament_Player",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentPlayers");

            migrationBuilder.DropColumn(
                name: "AllowLateRegistration",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "LateRegistrationMode",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OpeningMatchAwayTeamId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OpeningMatchHomeTeamId",
                table: "Tournaments");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tournaments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
