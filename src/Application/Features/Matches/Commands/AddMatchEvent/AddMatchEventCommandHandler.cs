using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Matches.Commands.AddMatchEvent;

public class AddMatchEventCommandHandler : IRequestHandler<AddMatchEventCommand, MatchDto>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<MatchEvent> _eventRepository;
    private readonly IRealTimeNotifier _notifier;
    private readonly IMapper _mapper;

    public AddMatchEventCommandHandler(
        IRepository<Match> matchRepository,
        IRepository<MatchEvent> eventRepository,
        IRealTimeNotifier notifier,
        IMapper mapper)
    {
        _matchRepository = matchRepository;
        _eventRepository = eventRepository;
        _notifier = notifier;
        _mapper = mapper;
    }

    public async Task<MatchDto> Handle(AddMatchEventCommand request, CancellationToken cancellationToken)
    {
        await ValidateManagementRights(request.Id, request.UserId, request.UserRole, cancellationToken);
        
        var match = await _matchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (match == null) throw new NotFoundException(nameof(Match), request.Id);

        if (!Enum.TryParse<MatchEventType>(request.Request.Type, true, out var eventType))
        {
            throw new BadRequestException("Invalid event type.");
        }

        var matchEvent = new MatchEvent
        {
            MatchId = request.Id,
            TeamId = request.Request.TeamId,
            PlayerId = request.Request.PlayerId,
            Type = eventType,
            Minute = request.Request.Minute
        };

        if (eventType == MatchEventType.Goal)
        {
            if (request.Request.TeamId == match.HomeTeamId)
                match.HomeScore++;
            else if (request.Request.TeamId == match.AwayTeamId)
                match.AwayScore++;
            
            await _matchRepository.UpdateAsync(match, cancellationToken);
        }

        await _eventRepository.AddAsync(matchEvent, cancellationToken);
        await _matchRepository.UpdateAsync(match, cancellationToken);

        var dto = _mapper.Map<MatchEventDto>(matchEvent);
        // Reload fresh read
        var updatedMatch = await _matchRepository.GetByIdAsync(request.Id, new[] { "Events.Player", "HomeTeam", "AwayTeam" }, cancellationToken);
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
