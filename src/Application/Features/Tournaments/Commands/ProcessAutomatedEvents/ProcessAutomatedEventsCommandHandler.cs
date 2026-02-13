using MediatR;
using Application.Interfaces;

namespace Application.Features.Tournaments.Commands.ProcessAutomatedEvents;

public class ProcessAutomatedEventsCommandHandler : IRequestHandler<ProcessAutomatedEventsCommand, Unit>
{
    private readonly ITournamentService _tournamentService;

    public ProcessAutomatedEventsCommandHandler(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
    }

    public async Task<Unit> Handle(ProcessAutomatedEventsCommand request, CancellationToken cancellationToken)
    {
        await _tournamentService.ProcessAutomatedStateTransitionsAsync();
        return Unit.Value;
    }
}
