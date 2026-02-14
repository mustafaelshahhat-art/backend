using MediatR;

namespace Application.Features.Tournaments.Commands.ResetSchedule;

public record ResetScheduleCommand(
    Guid TournamentId,
    Guid UserId,
    string UserRole) : IRequest<Unit>;
