using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.EliminateTeam;

public class EliminateTeamCommandHandler : IRequestHandler<EliminateTeamCommand, bool>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;

    public EliminateTeamCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
    }

    public async Task<bool> Handle(EliminateTeamCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        // Authorization
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
        if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, cancellationToken)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // Update registration status
        registration.Status = RegistrationStatus.Withdrawn; // Using Withdrawn as a proxy for elimination if no specific Enum exists
        await _registrationRepository.UpdateAsync(registration, cancellationToken);

        if (tournament.CurrentTeams > 0)
        {
            tournament.CurrentTeams--;
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        }

        return true;
    }
}
