using System.Threading;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Services;

public class SearchService : ISearchService
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<User> _userRepository;

    public SearchService(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<Team> teamRepository,
        IRepository<User> userRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _userRepository = userRepository;
    }

    public async Task<SearchResponse> SearchAsync(string query, string? userId, string role, CancellationToken ct = default)
    {
        var results = new List<SearchResultItem>();
        var normalizedQuery = query.ToLower().Trim();

        // Search tournaments (all roles can see)
        var tournaments = await _tournamentRepository.FindAsync(t =>
            t.Name.ToLower().Contains(normalizedQuery) ||
            (t.Description != null && t.Description.ToLower().Contains(normalizedQuery)), ct);

        foreach (var t in tournaments.Take(5))
        {
            var routePrefix = GetRoutePrefix(role);
            results.Add(new SearchResultItem
            {
                Id = t.Id.ToString(),
                Type = "tournament",
                Title = t.Name,
                Subtitle = t.Status.ToString(),
                Icon = "emoji_events",
                Route = $"{routePrefix}/tournaments/{t.Id}"
            });
        }

        // Search matches (filter by role)
        var matches = await GetMatchesForRole(normalizedQuery, userId, role, ct);
        foreach (var m in matches.Take(5))
        {
            var routePrefix = GetRoutePrefix(role);
            results.Add(new SearchResultItem
            {
                Id = m.Id.ToString(),
                Type = "match",
                Title = $"{m.HomeTeam?.Name ?? "Home"} vs {m.AwayTeam?.Name ?? "Away"}",
                Subtitle = m.Status.ToString(),
                Icon = "sports_soccer",
                Route = $"{routePrefix}/matches/{m.Id}"
            });
        }

        // Search teams (all roles can see)
        var teams = await _teamRepository.FindAsync(t =>
            t.Name.ToLower().Contains(normalizedQuery), ct);

        foreach (var t in teams.Take(5))
        {
            var routePrefix = GetRoutePrefix(role);
            results.Add(new SearchResultItem
            {
                Id = t.Id.ToString(),
                Type = "team",
                Title = t.Name,
                Subtitle = $"{t.Players?.Count ?? 0} لاعبين",
                Icon = "groups",
                Route = $"{routePrefix}/teams/{t.Id}"
            });
        }

        // Search users (Admin only)
        if (role == "Admin")
        {
            var users = await _userRepository.FindAsync(u =>
                u.Name.ToLower().Contains(normalizedQuery) ||
                u.Email.ToLower().Contains(normalizedQuery), ct);

            foreach (var u in users.Take(5))
            {
                results.Add(new SearchResultItem
                {
                    Id = u.Id.ToString(),
                    Type = "user",
                    Title = u.Name,
                    Subtitle = u.Role.ToString(),
                    Icon = "person",
                    Route = $"/admin/users/{u.Id}"
                });
            }
        }

        return new SearchResponse
        {
            Results = results.Take(15).ToList(),
            TotalCount = results.Count
        };
    }

    private async Task<IEnumerable<Match>> GetMatchesForRole(string query, string? userId, string role, CancellationToken ct = default)
    {
        var matches = await _matchRepository.FindAsync(m =>
            (m.HomeTeam != null && m.HomeTeam.Name.ToLower().Contains(query)) ||
            (m.AwayTeam != null && m.AwayTeam.Name.ToLower().Contains(query)),
            new[] { "HomeTeam", "AwayTeam" }, ct);

        if (role == "Admin")
        {
            return matches;
        }

        // Player: return all matches they might be interested in
        return matches;
    }

    private string GetRoutePrefix(string role)
    {
        return role switch
        {
            "Admin" => "/admin",
            _ => "/captain"
        };
    }
}
