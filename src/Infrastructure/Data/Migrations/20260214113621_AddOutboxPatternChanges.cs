using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxPatternChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ErrorCount",
                table: "OutboxMessages",
                newName: "RetryCount");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "OutboxMessages",
                newName: "Payload");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_ScheduledAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_ScheduledAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "OutboxMessages");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "OutboxMessages",
                newName: "ErrorCount");

            migrationBuilder.RenameColumn(
                name: "Payload",
                table: "OutboxMessages",
                newName: "Content");
        }
    }
}
