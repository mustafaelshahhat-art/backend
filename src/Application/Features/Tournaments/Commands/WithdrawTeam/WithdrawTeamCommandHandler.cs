using MediatR;
using Application.Interfaces;

namespace Application.Features.Tournaments.Commands.WithdrawTeam;

public record WithdrawTeamCommand(Guid TournamentId, Guid TeamId, Guid UserId) : IRequest<Unit>;

public class WithdrawTeamCommandHandler : IRequestHandler<WithdrawTeamCommand, Unit>
{
    private readonly ITournamentService _tournamentService;

    public WithdrawTeamCommandHandler(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
    }

    public async Task<Unit> Handle(WithdrawTeamCommand request, CancellationToken cancellationToken)
    {
        await _tournamentService.WithdrawTeamAsync(request.TournamentId, request.TeamId, request.UserId);
        return Unit.Value;
    }
}
