using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new columns first (before altering Type)
            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "Notifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "EntityId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Step 2: Migrate old string Type → Category enum values
            // Old Type was a string like "system","match","team","tournament", etc.
            // New Category enum: System=0, Account=1, Payments=2, Tournament=3, Match=4, Team=5, Administrative=6, Security=7
            migrationBuilder.Sql(@"
                UPDATE Notifications SET Category = CASE
                    WHEN [Type] IN ('system')                                                 THEN 0
                    WHEN [Type] IN ('account', 'account_approved', 'account_rejected')        THEN 1
                    WHEN [Type] IN ('payment', 'payments')                                    THEN 2
                    WHEN [Type] IN ('tournament', 'tournament_elimination', 
                                    'registration_approved', 'registration_rejected')         THEN 3
                    WHEN [Type] IN ('match', 'match_started', 'match_ended', 
                                    'match_event', 'match_score')                             THEN 4
                    WHEN [Type] IN ('team', 'team_disabled', 'team_activated', 
                                    'join_request', 'join_accepted', 'join_rejected',
                                    'invite', 'invite_accepted', 'invite_rejected',
                                    'team_removal')                                           THEN 5
                    WHEN [Type] IN ('admin', 'administrative')                                THEN 6
                    WHEN [Type] IN ('security')                                               THEN 7
                    ELSE 0
                END
            ");

            // Step 3: Convert Type column from string → int
            // New Type enum: Info=0, Success=1, Warning=2, Error=3
            // Each statement must be a separate Sql() call so SQL Server compiles them independently
            migrationBuilder.Sql(@"
                ALTER TABLE Notifications ADD [TypeNew] int NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                UPDATE Notifications SET [TypeNew] = CASE
                    WHEN [Type] IN ('account_approved', 'join_accepted', 'invite_accepted', 
                                    'team_activated', 'registration_approved')   THEN 1
                    WHEN [Type] IN ('team_disabled', 'account_rejected', 
                                    'registration_rejected', 'join_rejected', 
                                    'invite_rejected', 'team_removal')           THEN 2
                    WHEN [Type] IN ('tournament_elimination')                    THEN 3
                    ELSE 0
                END;
            ");

            migrationBuilder.Sql(@"ALTER TABLE Notifications DROP COLUMN [Type];");

            migrationBuilder.Sql(@"EXEC sp_rename 'Notifications.TypeNew', 'Type', 'COLUMN';");

            // Step 4: Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ExpiresAt",
                table: "Notifications",
                column: "ExpiresAt",
                filter: "[ExpiresAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_User_Category_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "Category", "CreatedAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_ExpiresAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_User_Category_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Notifications");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
