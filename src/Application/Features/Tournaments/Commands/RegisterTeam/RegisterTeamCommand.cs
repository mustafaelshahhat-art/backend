using MediatR;
using Application.DTOs.Tournaments;

namespace Application.Features.Tournaments.Commands.RegisterTeam;

public class RegisterTeamCommand : IRequest<TeamRegistrationDto>
{
    public Guid TournamentId { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }

    public RegisterTeamCommand(Guid tournamentId, Guid teamId, Guid userId)
    {
        TournamentId = tournamentId;
        TeamId = teamId;
        UserId = userId;
    }
}
