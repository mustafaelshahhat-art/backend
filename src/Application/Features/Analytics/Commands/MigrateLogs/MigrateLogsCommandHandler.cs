using Application.Interfaces;
using MediatR;

namespace Application.Features.Analytics.Commands.MigrateLogs;

public class MigrateLogsCommandHandler : IRequestHandler<MigrateLogsCommand, Unit>
{
    private readonly IActivityLogMigrationService _migrationService;

    public MigrateLogsCommandHandler(IActivityLogMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    public async Task<Unit> Handle(MigrateLogsCommand request, CancellationToken ct)
    {
        await _migrationService.MigrateLegacyLogsAsync(ct);
        return Unit.Value;
    }
}
