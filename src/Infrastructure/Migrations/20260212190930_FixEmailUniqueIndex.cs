using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEmailUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Identify duplicates
                SELECT Email, MIN(Id) as PrimaryId
                INTO #UserDups
                FROM Users
                GROUP BY Email
                HAVING COUNT(*) > 1;

                -- Re-assign references from duplicates to the primary record
                UPDATE T SET CreatorUserId = UD.PrimaryId
                FROM Tournaments T
                INNER JOIN Users U ON T.CreatorUserId = U.Id
                INNER JOIN #UserDups UD ON U.Email = UD.Email
                WHERE U.Id <> UD.PrimaryId;

                UPDATE A SET UserId = UD.PrimaryId
                FROM Activities A
                INNER JOIN Users U ON A.UserId = U.Id
                INNER JOIN #UserDups UD ON U.Email = UD.Email
                WHERE U.Id <> UD.PrimaryId;

                UPDATE P SET UserId = UD.PrimaryId
                FROM Players P
                INNER JOIN Users U ON P.UserId = U.Id
                INNER JOIN #UserDups UD ON U.Email = UD.Email
                WHERE U.Id <> UD.PrimaryId;

                UPDATE N SET UserId = UD.PrimaryId
                FROM Notifications N
                INNER JOIN Users U ON N.UserId = U.Id
                INNER JOIN #UserDups UD ON U.Email = UD.Email
                WHERE U.Id <> UD.PrimaryId;

                UPDATE O SET UserId = UD.PrimaryId
                FROM Otps O
                INNER JOIN Users U ON O.UserId = U.Id
                INNER JOIN #UserDups UD ON U.Email = UD.Email
                WHERE U.Id <> UD.PrimaryId;

                UPDATE TJR SET UserId = UD.PrimaryId
                FROM TeamJoinRequests TJR
                INNER JOIN Users U ON TJR.UserId = U.Id
                INNER JOIN #UserDups UD ON U.Email = UD.Email
                WHERE U.Id <> UD.PrimaryId;

                -- Finally delete duplicates
                DELETE FROM Users WHERE Id NOT IN (
                    SELECT MIN(Id) FROM Users 
                    GROUP BY Email
                );

                DROP TABLE #UserDups;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Users_Email",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
