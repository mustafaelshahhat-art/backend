using MediatR;

namespace Application.Features.Analytics.Commands.MigrateLogs;

public record MigrateLogsCommand() : IRequest<Unit>;
