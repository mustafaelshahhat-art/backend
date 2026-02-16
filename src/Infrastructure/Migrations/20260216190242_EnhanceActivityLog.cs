using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                table: "Activities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntityId",
                table: "Activities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "Activities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "Activities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ── Backfill Severity from existing Type codes ──
            migrationBuilder.Sql(@"
                UPDATE Activities SET Severity = 2
                WHERE Type IN ('MATCH_CANCELLED','TOURNAMENT_DELETED','TEAM_DISABLED','ADMIN_OVERRIDE');
            ");
            migrationBuilder.Sql(@"
                UPDATE Activities SET Severity = 1
                WHERE Type IN ('MATCH_SCORE_UPDATED','MATCH_EVENT_REMOVED','MATCH_POSTPONED',
                               'TEAM_ELIMINATED','TEAM_REMOVED','TEAM_DEACTIVATED','ADMIN_CREATED');
            ");

            // ── Backfill EntityType from existing Type codes ──
            migrationBuilder.Sql(@"
                UPDATE Activities SET EntityType = N'User'
                WHERE Type IN ('USER_REGISTERED','USER_LOGIN','ADMIN_CREATED','PASSWORD_CHANGED','AVATAR_UPDATED');
            ");
            migrationBuilder.Sql(@"
                UPDATE Activities SET EntityType = N'Match'
                WHERE Type IN ('MATCH_STARTED','MATCH_ENDED','MATCH_SCORE_UPDATED','MATCH_EVENT_ADDED',
                               'MATCH_EVENT_REMOVED','MATCH_POSTPONED','MATCH_RESCHEDULED','MATCH_CANCELLED');
            ");
            migrationBuilder.Sql(@"
                UPDATE Activities SET EntityType = N'Tournament'
                WHERE Type IN ('TOURNAMENT_CREATED','TOURNAMENT_GENERATED','TOURNAMENT_FINALIZED',
                               'TOURNAMENT_REGISTRATION_CLOSED','TOURNAMENT_DELETED','REGISTRATION_APPROVED',
                               'TEAM_ELIMINATED','GROUPS_FINISHED','KNOCKOUT_STARTED');
            ");
            migrationBuilder.Sql(@"
                UPDATE Activities SET EntityType = N'Team'
                WHERE Type IN ('TEAM_CREATED','TEAM_JOINED','TEAM_REMOVED','TEAM_DISABLED',
                               'TEAM_ACTIVATED','TEAM_DEACTIVATED');
            ");
            migrationBuilder.Sql(@"
                UPDATE Activities SET EntityType = N'Payment'
                WHERE Type IN ('PAYMENT_SUBMITTED','PAYMENT_APPROVED');
            ");
            migrationBuilder.Sql(@"
                UPDATE Activities SET EntityType = N'System'
                WHERE Type IN ('ADMIN_OVERRIDE','GUEST_VISIT');
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_CreatedAt_Desc",
                table: "Activities",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_EntityType_CreatedAt",
                table: "Activities",
                columns: new[] { "EntityType", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Severity_CreatedAt",
                table: "Activities",
                columns: new[] { "Severity", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_CreatedAt_Desc",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_EntityType_CreatedAt",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_Severity_CreatedAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Activities");
        }
    }
}
