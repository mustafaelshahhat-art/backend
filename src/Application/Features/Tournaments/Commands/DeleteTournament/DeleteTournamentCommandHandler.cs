using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.DeleteTournament;

public class DeleteTournamentCommandHandler : IRequestHandler<DeleteTournamentCommand, bool>
{
    private readonly IRepository<Tournament> _tournamentRepository;

    public DeleteTournamentCommandHandler(
        IRepository<Tournament> tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task<bool> Handle(DeleteTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.Id);

        // Authorization
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
        if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

        await _tournamentRepository.DeleteAsync(tournament, cancellationToken);
        
        return true;
    }
}
