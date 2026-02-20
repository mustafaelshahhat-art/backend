using Application.DTOs;
using Application.DTOs.Matches;
using Application.Features.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.SetOpeningMatch;

/// <summary>
/// PRE-DRAW: Sets two teams as the opening match BEFORE schedule generation.
/// - Only Creator/Admin.
/// - Only if matches not generated.
/// - Must run before GenerateScheduleCommand.
/// - Uses DistributedLock.
/// - Overrides previous opening selection if exists.
/// - Cannot be called after schedule generated.
/// - AUTOMATION: Automatically triggers match generation for Random scheduling mode.
/// </summary>
public class SetOpeningMatchCommandHandler : IRequestHandler<SetOpeningMatchCommand, MatchListResponse>
{
    private readonly ITournamentRegistrationContext _regContext;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;
    private readonly IDistributedCache _distributedCache;

    public SetOpeningMatchCommandHandler(
        ITournamentRegistrationContext regContext,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IRealTimeNotifier notifier,
        IDistributedCache distributedCache)
    {
        _regContext = regContext;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _notifier = notifier;
        _distributedCache = distributedCache;
    }

    public async Task<MatchListResponse> Handle(SetOpeningMatchCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _regContext.DistributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("العملية قيد التنفيذ من قبل مستخدم آخر.");
        }

        try
        {
            // Load WITH Registrations — CreateMatches needs tournament.Registrations to assign
            // each TeamRegistration.GroupId (group distribution in-memory, persisted by UpdateAsync).
            // Avoid a second GetByIdAsync call later; that would cause an EF Core identity-tracking
            // conflict (two Tournament instances with the same PK in the same DbContext scope).
            var tournament = await _regContext.Tournaments.GetByIdAsync(
                request.TournamentId,
                new[] { "Registrations", "Registrations.Team" },
                cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            // Authorization: Only Creator or Admin
            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner)
                throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            // Validate: No matches already generated
            var matchesExist = await _matchRepository.AnyAsync(m => m.TournamentId == request.TournamentId, cancellationToken);

            // STRICT: Validate all teams are approved (Payment Lock System)
            var allActiveRegistrations = await _regContext.Registrations.FindAsync(
                r => r.TournamentId == request.TournamentId && 
                     r.Status != RegistrationStatus.Rejected && 
                     r.Status != RegistrationStatus.Withdrawn &&
                     r.Status != RegistrationStatus.WaitingList, 
                cancellationToken);

            if (allActiveRegistrations.Any(r => r.Status != RegistrationStatus.Approved))
            {
                throw new ConflictException("بانتظار اكتمال الموافقة على جميع المدفوعات.");
            }

            // Ensure we have enough teams
            if (allActiveRegistrations.Count() < (tournament.MinTeams ?? 2))
            {
                 throw new ConflictException("عدد الفرق غير كاف لبدء البطولة.");
            }

            var registeredTeamIds = allActiveRegistrations.Select(r => r.TeamId);

            // Domain validation via entity method (encapsulated)
            tournament.SetOpeningTeams(request.HomeTeamId, request.AwayTeamId, registeredTeamIds, matchesExist);

            // AUTOMATION: If tournament is in Random scheduling mode, automatically generate matches.
            // IMPORTANT: CreateMatches MUST run before any UpdateAsync so that EF Core marks
            // TeamRegistration.GroupId as Modified *after* the value is set.
            // Calling UpdateAsync first (before CreateMatches) would save GroupId=null, and
            // while EF Core change-tracking should pick up the subsequent mutation, the safest
            // pattern is one single UpdateAsync after all in-memory changes are complete.
            MatchListResponse matchListResponse = new MatchListResponse(new List<MatchDto>());
            if (tournament.SchedulingMode == SchedulingMode.Random)
            {
                var teamIds = allActiveRegistrations.Select(r => r.TeamId).ToList();

                // Sets GroupId on each TeamRegistration in tournament.Registrations (tracked entities)
                var matches = TournamentHelper.CreateMatches(tournament, teamIds);

                tournament.ChangeStatus(TournamentStatus.Active);

                // Single UpdateAsync: _dbSet.Update marks tournament + ALL registrations as
                // Modified — GroupId is NOW set, so the DB saves the correct values.
                await _regContext.Tournaments.UpdateAsync(tournament, cancellationToken);

                // Save matches
                await _matchRepository.AddRangeAsync(matches);

                // Clear the standings distributed cache so GetStandingsHandler recomputes
                // fresh standings with the new GroupId assignments instead of serving
                // a stale 60-second TTL entry.
                await _distributedCache.RemoveAsync($"standings:{tournament.Id}", cancellationToken);

                var generatedMatches = _mapper.Map<IEnumerable<MatchDto>>(matches);
                await _notifier.SendMatchesGeneratedAsync(generatedMatches, cancellationToken);
                matchListResponse = new MatchListResponse(generatedMatches.ToList());
            }
            else
            {
                // Manual mode: persist the opening team selection only (no match generation).
                await _regContext.Tournaments.UpdateAsync(tournament, cancellationToken);
            }

            return matchListResponse;
        }
        finally
        {
            await _regContext.DistributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
