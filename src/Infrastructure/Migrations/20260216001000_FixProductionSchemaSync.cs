using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Idempotent migration to fix production schema drift.
    /// The PerformanceRefactor_TeamStats migration had these column renames commented out
    /// because they were "already applied" to dev DB — but they were NOT applied to production.
    /// This migration safely applies all missing schema changes using IF EXISTS/IF NOT EXISTS guards.
    /// </summary>
    public partial class FixProductionSchemaSync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =====================================================
            // 1. Rename SeedingMode → SchedulingMode (if old name still exists)
            // =====================================================
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Tournaments') AND name = 'SeedingMode'
                )
                BEGIN
                    EXEC sp_rename 'Tournaments.SeedingMode', 'SchedulingMode', 'COLUMN';
                END
            ");

            // =====================================================
            // 2. Rename OpeningMatchHomeTeamId → OpeningTeamAId (if old name still exists)
            // =====================================================
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Tournaments') AND name = 'OpeningMatchHomeTeamId'
                )
                BEGIN
                    EXEC sp_rename 'Tournaments.OpeningMatchHomeTeamId', 'OpeningTeamAId', 'COLUMN';
                END
            ");

            // =====================================================
            // 3. Rename OpeningMatchAwayTeamId → OpeningTeamBId (if old name still exists)
            // =====================================================
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Tournaments') AND name = 'OpeningMatchAwayTeamId'
                )
                BEGIN
                    EXEC sp_rename 'Tournaments.OpeningMatchAwayTeamId', 'OpeningTeamBId', 'COLUMN';
                END
            ");

            // =====================================================
            // 4. Drop QualifiedTeamsPerGroup if it still exists
            // =====================================================
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Tournaments') AND name = 'QualifiedTeamsPerGroup'
                )
                BEGIN
                    -- Drop default constraint first (SQL Server requires this before dropping a column)
                    DECLARE @constraintName NVARCHAR(256);
                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('Tournaments') AND c.name = 'QualifiedTeamsPerGroup';

                    IF @constraintName IS NOT NULL
                    BEGIN
                        DECLARE @sql NVARCHAR(MAX) = 'ALTER TABLE [Tournaments] DROP CONSTRAINT [' + @constraintName + ']';
                        EXEC sp_executesql @sql;
                    END

                    ALTER TABLE [Tournaments] DROP COLUMN [QualifiedTeamsPerGroup];
                END
            ");

            // =====================================================
            // 5. Add GroupId to TeamRegistrations if missing
            // =====================================================
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('TeamRegistrations') AND name = 'GroupId'
                )
                BEGIN
                    ALTER TABLE [TeamRegistrations] ADD [GroupId] INT NULL;
                END
            ");

            // =====================================================
            // 6. Add IsOpeningMatch to Matches if missing
            // =====================================================
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Matches') AND name = 'IsOpeningMatch'
                )
                BEGIN
                    ALTER TABLE [Matches] ADD [IsOpeningMatch] BIT NOT NULL DEFAULT 0;
                END
            ");

            // =====================================================
            // 7. Alter Tournaments.Name to nvarchar(450) if still nvarchar(max)
            //    (Required for unique index)
            // =====================================================
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns c
                    JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID('Tournaments') 
                      AND c.name = 'Name' 
                      AND c.max_length = -1
                )
                BEGIN
                    ALTER TABLE [Tournaments] ALTER COLUMN [Name] NVARCHAR(450) NOT NULL;
                END
            ");

            // =====================================================
            // 8. Create missing indexes (idempotent)
            // =====================================================
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_CreatedAt' AND object_id = OBJECT_ID('Users'))
                    CREATE INDEX [IX_Users_CreatedAt] ON [Users]([CreatedAt]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tournaments_CreatedAt' AND object_id = OBJECT_ID('Tournaments'))
                    CREATE INDEX [IX_Tournaments_CreatedAt] ON [Tournaments]([CreatedAt]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tournaments_StartDate' AND object_id = OBJECT_ID('Tournaments'))
                    CREATE INDEX [IX_Tournaments_StartDate] ON [Tournaments]([StartDate]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Tournaments_Name' AND object_id = OBJECT_ID('Tournaments'))
                    CREATE UNIQUE INDEX [UQ_Tournaments_Name] ON [Tournaments]([Name]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamRegistration_CreatedAt' AND object_id = OBJECT_ID('TeamRegistrations'))
                    CREATE INDEX [IX_TeamRegistration_CreatedAt] ON [TeamRegistrations]([CreatedAt]);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down is intentionally empty — this migration is a one-way fix.
            // The original PerformanceRefactor_TeamStats.Down already handles the full rollback.
        }
    }
}
