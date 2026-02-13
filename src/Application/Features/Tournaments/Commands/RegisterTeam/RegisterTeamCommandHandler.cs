using MediatR;
using Application.DTOs.Tournaments;
using Application.Interfaces;

namespace Application.Features.Tournaments.Commands.RegisterTeam;

public class RegisterTeamCommandHandler : IRequestHandler<RegisterTeamCommand, TeamRegistrationDto>
{
    private readonly ITournamentService _tournamentService;

    public RegisterTeamCommandHandler(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
    }

    public async Task<TeamRegistrationDto> Handle(RegisterTeamCommand request, CancellationToken cancellationToken)
    {
        // The service logic will now be transaction-agnostic (stripped of BeginTransaction)
        // The TransactionBehavior pipeline handles the transaction for this Command.
        
        var registrationRequest = new RegisterTeamRequest { TeamId = request.TeamId };
        return await _tournamentService.RegisterTeamAsync(request.TournamentId, registrationRequest, request.UserId);
    }
}
