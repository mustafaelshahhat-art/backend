using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Matches.Commands.RemoveMatchEvent;

public class RemoveMatchEventCommandHandler : IRequestHandler<RemoveMatchEventCommand, MatchDto>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<MatchEvent> _eventRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;

    public RemoveMatchEventCommandHandler(
        IRepository<Match> matchRepository,
        IRepository<MatchEvent> eventRepository,
        IMapper mapper,
        IRealTimeNotifier notifier)
    {
        _matchRepository = matchRepository;
        _eventRepository = eventRepository;
        _mapper = mapper;
        _notifier = notifier;
    }

    public async Task<MatchDto> Handle(RemoveMatchEventCommand request, CancellationToken cancellationToken)
    {
        await ValidateManagementRights(request.MatchId, request.UserId, request.UserRole, cancellationToken);
        
        var match = await _matchRepository.GetByIdAsync(request.MatchId, cancellationToken);
        if (match == null) throw new NotFoundException(nameof(Match), request.MatchId);

        var matchEvent = await _eventRepository.GetByIdAsync(request.EventId, cancellationToken);
        if (matchEvent == null || matchEvent.MatchId != request.MatchId) throw new NotFoundException(nameof(MatchEvent), request.EventId);

        if (matchEvent.Type == MatchEventType.Goal)
        {
            if (matchEvent.TeamId == match.HomeTeamId)
                match.HomeScore = Math.Max(0, match.HomeScore - 1);
            else if (matchEvent.TeamId == match.AwayTeamId)
                match.AwayScore = Math.Max(0, match.AwayScore - 1);
            
            await _matchRepository.UpdateAsync(match, cancellationToken);
        }

        await _eventRepository.DeleteAsync(matchEvent, cancellationToken);

        // Reload fresh read
        var updatedMatch = await _matchRepository.GetByIdAsync(request.MatchId, new[] { "Events.Player", "HomeTeam", "AwayTeam" }, cancellationToken);
        var matchDto = _mapper.Map<MatchDto>(updatedMatch);
        await _notifier.SendMatchUpdatedAsync(matchDto, cancellationToken);

        return matchDto;
    }

    private async Task ValidateManagementRights(Guid matchId, Guid userId, string userRole, CancellationToken ct)
    {
        var isAdmin = userRole == UserRole.Admin.ToString();
        if (isAdmin) return;

        var match = await _matchRepository.GetByIdAsync(matchId, new[] { "Tournament" }, ct);
        if (match == null) throw new NotFoundException(nameof(Match), matchId);

        if (userRole != UserRole.TournamentCreator.ToString() || match.Tournament?.CreatorUserId != userId)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة هذا اللقاء. فقط منظم البطولة يمكنه ذلك.");
        }
    }
}
