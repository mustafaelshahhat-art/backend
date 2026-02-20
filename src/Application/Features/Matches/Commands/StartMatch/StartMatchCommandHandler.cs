using Application.Common;
using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Matches.Commands.StartMatch;

public class StartMatchCommandHandler : IRequestHandler<StartMatchCommand, MatchDto>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IMapper _mapper;
    private readonly IMatchEventNotifier _matchEventNotifier;

    public StartMatchCommandHandler(
        IRepository<Match> matchRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IMatchEventNotifier matchEventNotifier)
    {
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _matchEventNotifier = matchEventNotifier;
    }

    public async Task<MatchDto> Handle(StartMatchCommand request, CancellationToken cancellationToken)
    {
        await ValidateManagementRights(request.Id, request.UserId, request.UserRole, cancellationToken);
        
        var match = await _matchRepository.GetByIdAsync(request.Id, new[] { "HomeTeam", "AwayTeam", "Tournament" }, cancellationToken);
        if (match == null) throw new NotFoundException(nameof(Match), request.Id);

        match.Status = MatchStatus.Live;
        await _matchRepository.UpdateAsync(match, cancellationToken);

        await _matchEventNotifier.LogActivityAsync(
            ActivityConstants.MATCH_STARTED, 
            new Dictionary<string, string> { { "matchInfo", $"{match.HomeTeam?.Name ?? "فريق"} ضد {match.AwayTeam?.Name ?? "فريق"}" } }, 
            null, 
            "نظام",
            cancellationToken
        );

        await _matchEventNotifier.SendMatchUpdateAsync(match, cancellationToken);

        return _mapper.Map<MatchDto>(match);
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
