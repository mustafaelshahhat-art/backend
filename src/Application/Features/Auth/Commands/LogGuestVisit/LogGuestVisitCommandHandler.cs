using Application.Common;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Auth.Commands.LogGuestVisit;

public class LogGuestVisitCommandHandler : IRequestHandler<LogGuestVisitCommand, Unit>
{
    private readonly IActivityLogger _activityLogger;

    public LogGuestVisitCommandHandler(IActivityLogger activityLogger) => _activityLogger = activityLogger;

    public async Task<Unit> Handle(LogGuestVisitCommand request, CancellationToken ct)
    {
        await _activityLogger.LogAsync(
            ActivityConstants.GUEST_VISIT,
            new Dictionary<string, string>(),
            null, "ضيف", ct);
        return Unit.Value;
    }
}
