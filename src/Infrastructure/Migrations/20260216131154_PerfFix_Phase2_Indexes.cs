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
            // ── Idempotent: some statements may already exist from a partial run ──

            // Drop old Otps index (IF EXISTS for idempotency)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Otps_UserId' AND object_id = OBJECT_ID('Otps'))
                    DROP INDEX [IX_Otps_UserId] ON [Otps];");

            // Narrow RefreshToken to nvarchar(450) so it can be indexed
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns c
                    JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID('Users') AND c.name = 'RefreshToken' AND c.max_length = -1
                )
                BEGIN
                    ALTER TABLE [Users] ALTER COLUMN [RefreshToken] NVARCHAR(450) NULL;
                END");

            // Narrow Otps.Type to nvarchar(450)
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns c
                    JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID('Otps') AND c.name = 'Type' AND c.max_length = -1
                )
                BEGIN
                    ALTER TABLE [Otps] ALTER COLUMN [Type] NVARCHAR(450) NOT NULL;
                END");

            // Narrow TeamJoinRequests.Status to nvarchar(50) so it can be indexed
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns c
                    JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID('TeamJoinRequests') AND c.name = 'Status' AND c.max_length = -1
                )
                BEGIN
                    ALTER TABLE [TeamJoinRequests] ALTER COLUMN [Status] NVARCHAR(50) NOT NULL;
                END");

            // Idempotent index creation
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_RefreshToken_Filtered' AND object_id = OBJECT_ID('Users'))
                    CREATE INDEX [IX_Users_RefreshToken_Filtered] ON [Users] ([RefreshToken]) WHERE [RefreshToken] IS NOT NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamRegistration_Tournament_Status' AND object_id = OBJECT_ID('TeamRegistrations'))
                    CREATE INDEX [IX_TeamRegistration_Tournament_Status] ON [TeamRegistrations] ([TournamentId], [Status]);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_User_Status' AND object_id = OBJECT_ID('TeamJoinRequests'))
                    CREATE INDEX [IX_TeamJoinRequests_User_Status] ON [TeamJoinRequests] ([UserId], [Status]);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Otps_User_Type_IsUsed' AND object_id = OBJECT_ID('Otps'))
                    CREATE INDEX [IX_Otps_User_Type_IsUsed] ON [Otps] ([UserId], [Type], [IsUsed]);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_User_CreatedAt' AND object_id = OBJECT_ID('Notifications'))
                    CREATE INDEX [IX_Notifications_User_CreatedAt] ON [Notifications] ([UserId], [CreatedAt] DESC);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MatchMessages_Match_Timestamp' AND object_id = OBJECT_ID('MatchMessages'))
                    CREATE INDEX [IX_MatchMessages_Match_Timestamp] ON [MatchMessages] ([MatchId], [Timestamp]);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Matches_Tournament_Status_Date' AND object_id = OBJECT_ID('Matches'))
                    CREATE INDEX [IX_Matches_Tournament_Status_Date] ON [Matches] ([TournamentId], [Status], [Date]);");
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
