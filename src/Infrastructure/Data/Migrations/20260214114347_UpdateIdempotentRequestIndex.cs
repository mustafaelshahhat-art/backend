using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIdempotentRequestIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_IdempotentRequests_Key",
                table: "IdempotentRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Route",
                table: "IdempotentRequests",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "UQ_IdempotentRequests_Key_Route",
                table: "IdempotentRequests",
                columns: new[] { "Key", "Route" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_IdempotentRequests_Key_Route",
                table: "IdempotentRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Route",
                table: "IdempotentRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "UQ_IdempotentRequests_Key",
                table: "IdempotentRequests",
                column: "Key",
                unique: true);
        }
    }
}
