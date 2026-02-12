using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardeningAndOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Initial Cleanups to handle pre-existing duplicate data before adding unique constraints
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Users_Email' AND object_id = OBJECT_ID('Users')) DROP INDEX UQ_Users_Email ON Users");
            
            migrationBuilder.Sql(@"
                DELETE FROM Players WHERE Id NOT IN (
                    SELECT MIN(Id) FROM Players 
                    GROUP BY TeamId, UserId
                ) AND UserId IS NOT NULL;
                
                DELETE FROM TeamRegistrations WHERE Id NOT IN (
                    SELECT MIN(Id) FROM TeamRegistrations 
                    GROUP BY TournamentId, TeamId
                );
            ");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_CreatorUserId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_TeamRegistrations_TournamentId",
                table: "TeamRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_Players_TeamId",
                table: "Players");

            migrationBuilder.RenameIndex(
                name: "IX_Players_UserId",
                table: "Players",
                newName: "IX_Players_User");

            migrationBuilder.AlterColumn<string>(
                name: "Neighborhood",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Governorate",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tournaments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tournaments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Teams",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TeamRegistrations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TeamJoinRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SystemSettings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Players",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Otps",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Notifications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MatchMessages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MatchEvents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Matches",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Activities",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Activities",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Location",
                table: "Users",
                columns: new[] { "Governorate", "City", "Neighborhood" });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Creator_Status",
                table: "Tournaments",
                columns: new[] { "CreatorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_TeamRegistration_Tournament_Team",
                table: "TeamRegistrations",
                columns: new[] { "TournamentId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Player_Team_User",
                table: "Players",
                columns: new[] { "TeamId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Type",
                table: "Activities",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_User_Date",
                table: "Activities",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Location",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_Creator_Status",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "UQ_TeamRegistration_Tournament_Team",
                table: "TeamRegistrations");

            migrationBuilder.DropIndex(
                name: "UQ_Player_Team_User",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Activities_Type",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_User_Date",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TeamRegistrations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TeamJoinRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Otps");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MatchMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MatchEvents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Activities");

            migrationBuilder.RenameIndex(
                name: "IX_Players_User",
                table: "Players",
                newName: "IX_Players_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Neighborhood",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Governorate",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tournaments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatorUserId",
                table: "Tournaments",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamRegistrations_TournamentId",
                table: "TeamRegistrations",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");
        }
    }
}
