using System.Threading;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
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

    public async Task<SearchResponse> SearchAsync(string query, int page, int pageSize, string? userId, string role, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        
        var normalizedQuery = query.Trim();
        if (string.IsNullOrEmpty(normalizedQuery)) return new SearchResponse();

        var routePrefix = GetRoutePrefix(role);
        int limitPerCategory = 20; // Fetch small set per category for unified view

        // 1. Search Tournaments (SARGABLE Prefix Match)
        var tournamentResults = await _tournamentRepository.GetQueryable()
            .AsNoTracking()
            .Where(t => t.Name.StartsWith(normalizedQuery))
            .OrderBy(t => t.Name)
            .Take(limitPerCategory)
            .Select(t => new SearchResultItem
            {
                Id = t.Id.ToString(),
                Type = "tournament",
                Title = t.Name,
                Subtitle = t.Status.ToString(),
                Icon = "emoji_events",
                Route = $"{routePrefix}/tournaments/{t.Id}"
            })
            .ToListAsync(ct);

        // 2. Search Teams
        var teamResults = await _teamRepository.GetQueryable()
            .AsNoTracking()
            .Where(t => t.Name.StartsWith(normalizedQuery))
            .OrderBy(t => t.Name)
            .Take(limitPerCategory)
            .Select(t => new SearchResultItem
            {
                Id = t.Id.ToString(),
                Type = "team",
                Title = t.Name,
                Subtitle = $"{t.Players.Count} لاعبين",
                Icon = "groups",
                Route = $"{routePrefix}/teams/{t.Id}"
            })
            .ToListAsync(ct);

        // 3. Search Users (Admin only)
        var userResults = new List<SearchResultItem>();
        if (role == "Admin")
        {
            userResults = await _userRepository.GetQueryable()
                .AsNoTracking()
                .Where(u => u.Name.StartsWith(normalizedQuery) || u.Email.StartsWith(normalizedQuery))
                .OrderBy(u => u.Name)
                .Take(limitPerCategory)
                .Select(u => new SearchResultItem
                {
                    Id = u.Id.ToString(),
                    Type = "user",
                    Title = u.Name,
                    Subtitle = u.Role.ToString(),
                    Icon = "person",
                    Route = $"/admin/users/{u.Id}"
                })
                .ToListAsync(ct);
        }

        var allResults = tournamentResults.Concat(teamResults).Concat(userResults).ToList();
        
        var totalCount = allResults.Count;
        var pagedResults = allResults
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResponse
        {
            Results = pagedResults,
            TotalCount = totalCount
        };
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
