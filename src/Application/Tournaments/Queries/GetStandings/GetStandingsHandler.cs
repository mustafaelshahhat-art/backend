using System.Text.Json;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace Application.Tournaments.Queries.GetStandings;

/// <summary>
/// Handles GetStandingsQuery using the extracted StandingsCalculator domain service.
/// 
/// This is the CLEAN pattern: handler orchestrates, domain calculates.
/// 
/// Dependencies: 3 (repos + cache)
/// - NO IAnalyticsService
/// - NO INotificationService
/// - NO IRealTimeNotifier
/// 
/// Compared to TournamentService.GetStandingsAsync which had 12 constructor deps
/// and mixed standings calculation with caching and service delegation.
/// </summary>
public class GetStandingsHandler
    : IRequestHandler<GetStandingsQuery, Application.Common.Models.PagedResult<TournamentStandingDto>>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IDistributedCache _distributedCache;

    public GetStandingsHandler(
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IDistributedCache distributedCache)
    {
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _distributedCache = distributedCache;
    }

    public async Task<Application.Common.Models.PagedResult<TournamentStandingDto>> Handle(
        GetStandingsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var cacheKey = $"standings:{request.TournamentId}";

        List<TournamentStandingDto>? allStandings = null;
        var cachedJson = await _distributedCache.GetStringAsync(cacheKey, ct);
        if (cachedJson != null)
        {
            allStandings = JsonSerializer.Deserialize<List<TournamentStandingDto>>(cachedJson);
        }

        if (allStandings == null)
        {
            // PERF: Replaced GetNoTrackingAsync + Include("Events") with targeted SQL projections.
            //
            // BEFORE (full entity load):
            //   SELECT * FROM Matches + LEFT JOIN MatchEvents  -- all 20+ columns each
            //   SELECT * FROM TeamRegistrations + LEFT JOIN Teams -- all columns
            //
            // AFTER (column-targeted projection):
            //   SELECT only HomeTeamId,AwayTeamId,HomeScore,AwayScore,Status,GroupId,StageName
            //   + nested SELECT TeamId,Type FROM MatchEvents — 9 columns total
            //   SELECT only TeamId,Team.Name,GroupId FROM TeamRegistrations
            //
            // Estimated SQL bandwidth reduction: 65-80% for a 16-team, 40-match tournament.

            // Step 1: Project matches to anonymous type (EF Core translates safely)
            var rawMatches = await _matchRepository.ExecuteQueryAsync(
                _matchRepository.GetQueryable()
                    .Where(m => m.TournamentId == request.TournamentId)
                    .Select(m => new
                    {
                        m.HomeTeamId,
                        m.AwayTeamId,
                        m.HomeScore,
                        m.AwayScore,
                        m.Status,
                        m.GroupId,
                        m.StageName,
                        // Only TeamId + Type from events — skip Id, MatchId, PlayerId, Minute, Description, timestamps
                        Events = m.Events.Select(e => new { e.TeamId, e.Type }).ToList()
                    }), ct);

            // Step 2: Map to StandingsCalculator input types in C# (no DB round-trip)
            var matchInputs = rawMatches.Select(m => new StandingsCalculator.MatchInput(
                m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore,
                m.Status, m.GroupId, m.StageName,
                m.Events.Select(e => new StandingsCalculator.MatchEventInput(e.TeamId, e.Type)).ToList()
            )).ToList();

            // Step 3: Project registrations — only TeamId, Team.Name, GroupId (no full Team entity)
            var registrationInputs = await _registrationRepository.ExecuteQueryAsync(
                _registrationRepository.GetQueryable()
                    .Where(r => r.TournamentId == request.TournamentId &&
                        (r.Status == RegistrationStatus.Approved ||
                         r.Status == RegistrationStatus.Withdrawn ||
                         r.Status == RegistrationStatus.Eliminated))
                    .Select(r => new StandingsCalculator.RegistrationInput(
                        r.TeamId,
                        r.Team != null ? r.Team.Name : "\u0641\u0631\u064a\u0642",
                        r.GroupId)), ct);

            // 4. Delegate to PURE domain service (zero deps, unit-testable)
            var standings = StandingsCalculator.Calculate(matchInputs, registrationInputs);

            // 3. Map to DTOs
            // GoalDifference is a computed property on the DTO (GoalsFor - GoalsAgainst)
            allStandings = standings.Select(s => new TournamentStandingDto
            {
                TeamId = s.TeamId,
                TeamName = s.TeamName,
                GroupId = s.GroupId,
                Played = s.Played,
                Won = s.Won,
                Drawn = s.Drawn,
                Lost = s.Lost,
                GoalsFor = s.GoalsFor,
                GoalsAgainst = s.GoalsAgainst,
                Points = s.Points,
                YellowCards = s.YellowCards,
                RedCards = s.RedCards,
                Rank = s.Rank,
                Form = s.Form
            }).ToList();

            // 4. Cache
            var cacheOpts = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
            var json = JsonSerializer.Serialize(allStandings);
            await _distributedCache.SetStringAsync(cacheKey, json, cacheOpts, ct);
        }

        // 5. Filter + paginate
        var query = allStandings.AsQueryable();
        if (request.GroupId.HasValue)
            query = query.Where(s => s.GroupId == request.GroupId.Value);

        var totalCount = query.Count();
        var items = query
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new Application.Common.Models.PagedResult<TournamentStandingDto>(
            items, totalCount, request.Page, pageSize);
    }
}
