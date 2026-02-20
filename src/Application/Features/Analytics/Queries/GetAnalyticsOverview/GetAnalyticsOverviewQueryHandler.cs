using System.Linq.Expressions;
using System.Text.Json;
using Application.Common;
using Application.DTOs.Analytics;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Shared.Exceptions;

namespace Application.Features.Analytics.Queries.GetAnalyticsOverview;

public class GetAnalyticsOverviewQueryHandler : IRequestHandler<GetAnalyticsOverviewQuery, object>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Activity> _activityRepository;
    private readonly IDistributedCache _distributedCache;

    public GetAnalyticsOverviewQueryHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<User> userRepository,
        IRepository<Team> teamRepository,
        IRepository<Player> playerRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Activity> activityRepository,
        IDistributedCache distributedCache)
    {
        _tournamentRepository = tournamentRepository;
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _activityRepository = activityRepository;
        _distributedCache = distributedCache;
    }

    public async Task<object> Handle(GetAnalyticsOverviewQuery request, CancellationToken ct)
    {
        var isAdmin = request.UserRole == UserRole.Admin.ToString();

        if (request.TeamId.HasValue)
        {
            if (!isAdmin)
            {
                var team = await _teamRepository.GetByIdNoTrackingAsync(request.TeamId.Value,
                    new Expression<Func<Team, object>>[] { t => t.Players }, ct);
                if (team == null) throw new NotFoundException("Team", request.TeamId.Value);
                if (!team.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain))
                    throw new ForbiddenException("Not authorized to view team analytics");
            }

            return await GetTeamAnalyticsAsync(request.TeamId.Value, ct);
        }

        var isCreator = request.UserRole == UserRole.TournamentCreator.ToString();
        if (!isAdmin && !isCreator)
            throw new ForbiddenException("Not authorized to view analytics");

        Guid? creatorId = isCreator ? request.UserId : null;
        return await GetOverviewAsync(creatorId, ct);
    }

    private async Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId, CancellationToken ct)
    {
        // PERF: Use range comparison instead of .Date equality — .Date prevents index seeks.
        // a.CreatedAt.Date == today → a.CreatedAt >= todayStart && a.CreatedAt < todayEnd
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        if (creatorId.HasValue)
        {
            var tournamentStats = await _tournamentRepository.ExecuteQueryAsync(
                _tournamentRepository.GetQueryable()
                .Where(t => t.CreatorUserId == creatorId.Value)
                .Select(t => new
                {
                    IsActive = t.Status == TournamentStatus.RegistrationOpen || t.Status == TournamentStatus.Active,
                    TeamCount = t.Registrations.Count(r => r.Status == RegistrationStatus.Approved),
                    // PERF: Range comparison (sargable) instead of .Date equality
                    MatchCountToday = t.Matches.Count(m => m.Date.HasValue && m.Date.Value >= todayStart && m.Date.Value < todayEnd),
                    Goals = t.Matches.Where(m => m.Status == MatchStatus.Finished).Sum(m => (int?)m.HomeScore + (int?)m.AwayScore) ?? 0
                }), ct);

            return new AnalyticsOverview
            {
                ActiveTournaments = tournamentStats.Count(x => x.IsActive),
                TotalTeams = tournamentStats.Sum(x => x.TeamCount),
                MatchesToday = tournamentStats.Sum(x => x.MatchCountToday),
                TotalGoals = tournamentStats.Sum(x => x.Goals),
                TotalUsers = 0,
                LoginsToday = 0
            };
        }

        var cacheKey = "analytics:admin_overview";
        var cachedJson = await _distributedCache.GetStringAsync(cacheKey, ct);
        if (cachedJson != null)
        {
            var cachedOverview = JsonSerializer.Deserialize<AnalyticsOverview>(cachedJson);
            if (cachedOverview != null) return cachedOverview;
        }

        // PERF: Consolidate 2 Match table queries (matchesToday COUNT + totalGoals SUM) into
        // 1 GroupBy aggregate query. Reduces from 6 sequential queries → 5.
        // The remaining 4 single-table COUNTs must stay separate (different tables,
        // EF Core DbContext is not safe for concurrent awaits on the same instance).
        var matchStats = await _matchRepository.ExecuteFirstOrDefaultAsync(
            _matchRepository.GetQueryable()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    MatchesToday = g.Count(m => m.Date.HasValue && m.Date.Value >= todayStart && m.Date.Value < todayEnd),
                    TotalGoals = (int)(g.Where(m => m.Status == MatchStatus.Finished)
                                        .Sum(m => (int?)m.HomeScore + (int?)m.AwayScore) ?? 0)
                }), ct);

        var activeTournaments = await _tournamentRepository.CountAsync(t => t.Status == TournamentStatus.RegistrationOpen || t.Status == TournamentStatus.Active, ct);
        var users = await _userRepository.CountAsync(u => u.IsEmailVerified, ct);
        var teams = await _teamRepository.CountAsync(_ => true, ct);
        // PERF: Sargable range comparison instead of CreatedAt.Date == today
        var loginsToday = await _activityRepository.CountAsync(a =>
            (a.Type == ActivityConstants.USER_LOGIN || a.Type == ActivityConstants.GUEST_VISIT)
            && a.CreatedAt >= todayStart && a.CreatedAt < todayEnd, ct);

        var overview = new AnalyticsOverview
        {
            TotalUsers = users,
            TotalTeams = teams,
            ActiveTournaments = activeTournaments,
            MatchesToday = matchStats?.MatchesToday ?? 0,
            LoginsToday = loginsToday,
            TotalGoals = matchStats?.TotalGoals ?? 0
        };

        var cacheOpts = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
        await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(overview), cacheOpts, ct);

        return overview;
    }

    private async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId, CancellationToken ct)
    {
        // PERF: Cache team analytics for 60s — was running 3 sequential COUNT queries on every request.
        // 3 single-table COUNT queries × throttled shared SQL = measurable latency spike per call.
        var cacheKey = $"analytics:team:{teamId}";
        var cachedJson = await _distributedCache.GetStringAsync(cacheKey, ct);
        if (cachedJson != null)
        {
            var cached = JsonSerializer.Deserialize<TeamAnalyticsDto>(cachedJson);
            if (cached != null) return cached;
        }

        var playerCount = await _playerRepository.CountAsync(p => p.TeamId == teamId, ct);
        var matchCount = await _matchRepository.CountAsync(m =>
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId) &&
            m.Status == MatchStatus.Scheduled, ct);
        var tournamentCount = await _registrationRepository.CountAsync(r =>
            r.TeamId == teamId &&
            r.Status == RegistrationStatus.Approved &&
            (r.Tournament != null && (r.Tournament.Status == TournamentStatus.Active || r.Tournament.Status == TournamentStatus.RegistrationOpen)), ct);

        var result = new TeamAnalyticsDto
        {
            PlayerCount = playerCount,
            UpcomingMatches = matchCount,
            ActiveTournaments = tournamentCount,
            Rank = "N/A"
        };

        var cacheOpts = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
        await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOpts, ct);

        return result;
    }
}
