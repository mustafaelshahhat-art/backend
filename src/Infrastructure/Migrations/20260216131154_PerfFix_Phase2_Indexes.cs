using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerfFix_Phase2_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Otps_UserId",
                table: "Otps");

            migrationBuilder.AlterColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Otps",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshToken_Filtered",
                table: "Users",
                column: "RefreshToken",
                filter: "[RefreshToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamRegistration_Tournament_Status",
                table: "TeamRegistrations",
                columns: new[] { "TournamentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamJoinRequests_User_Status",
                table: "TeamJoinRequests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Otps_User_Type_IsUsed",
                table: "Otps",
                columns: new[] { "UserId", "Type", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_User_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MatchMessages_Match_Timestamp",
                table: "MatchMessages",
                columns: new[] { "MatchId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Tournament_Status_Date",
                table: "Matches",
                columns: new[] { "TournamentId", "Status", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_RefreshToken_Filtered",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TeamRegistration_Tournament_Status",
                table: "TeamRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_TeamJoinRequests_User_Status",
                table: "TeamJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_Otps_User_Type_IsUsed",
                table: "Otps");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_User_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_MatchMessages_Match_Timestamp",
                table: "MatchMessages");

            migrationBuilder.DropIndex(
                name: "IX_Matches_Tournament_Status_Date",
                table: "Matches");

            migrationBuilder.AlterColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Otps",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_Otps_UserId",
                table: "Otps",
                column: "UserId");
        }
    }
}
