namespace Application.Interfaces;

public interface IActivityLogMigrationService
{
    Task MigrateLegacyLogsAsync(CancellationToken ct = default);
}
