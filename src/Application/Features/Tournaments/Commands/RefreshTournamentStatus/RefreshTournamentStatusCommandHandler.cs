using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using Application.DTOs.Tournaments;

namespace Application.Features.Tournaments.Commands.RefreshTournamentStatus;

public class RefreshTournamentStatusCommandHandler : IRequestHandler<RefreshTournamentStatusCommand, TournamentLifecycleResult>
{
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IRepository<Tournament> _tournamentRepository;

    public RefreshTournamentStatusCommandHandler(
        ITournamentLifecycleService lifecycleService,
        IRepository<Tournament> tournamentRepository)
    {
        _lifecycleService = lifecycleService;
        _tournamentRepository = tournamentRepository;
    }

    public async Task<TournamentLifecycleResult> Handle(RefreshTournamentStatusCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null)
            throw new NotFoundException(nameof(Tournament), request.TournamentId);

        // Force a re-check of the tournament status based on match results
        var result = await _lifecycleService.CheckAndFinalizeTournamentAsync(request.TournamentId, cancellationToken);
        
        return result;
    }
}
