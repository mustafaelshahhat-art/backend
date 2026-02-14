using MediatR;
using Application.DTOs.Tournaments;
using Application.Interfaces;

namespace Application.Features.Tournaments.Commands.ApproveRegistration;

public record ApproveRegistrationCommand(Guid TournamentId, Guid TeamId, Guid UserId, string UserRole) : IRequest<TeamRegistrationDto>;

public class ApproveRegistrationCommandHandler : IRequestHandler<ApproveRegistrationCommand, TeamRegistrationDto>
{
    private readonly ITournamentService _tournamentService;

    public ApproveRegistrationCommandHandler(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
    }

    public async Task<TeamRegistrationDto> Handle(ApproveRegistrationCommand request, CancellationToken cancellationToken)
    {
        return await _tournamentService.ApproveRegistrationAsync(request.TournamentId, request.TeamId, request.UserId, request.UserRole, cancellationToken);
    }
}
