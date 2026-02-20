using Application.Common.Models;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Search.Queries;

public class SearchQueryHandler : IRequestHandler<SearchQuery, PagedResult<SearchResultItem>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<User> _userRepository;

    public SearchQueryHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Team> teamRepository,
        IRepository<User> userRepository)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _userRepository = userRepository;
    }

    public async Task<PagedResult<SearchResultItem>> Handle(SearchQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize > 100 ? 100 : request.PageSize;
        var normalizedQuery = request.Query.Trim();
        if (string.IsNullOrEmpty(normalizedQuery)) return new PagedResult<SearchResultItem>(new List<SearchResultItem>(), 0, request.Page, pageSize);

        var routePrefix = request.Role == "Admin" ? "/admin" : "/captain";
        int limitPerCategory = 20;

        // 1. Tournaments (SARGABLE prefix match)
        var tournamentResults = await _tournamentRepository.ExecuteQueryAsync(
            _tournamentRepository.GetQueryable()
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
            }), cancellationToken);

        // 2. Teams
        var teamResults = await _teamRepository.ExecuteQueryAsync(
            _teamRepository.GetQueryable()
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
            }), cancellationToken);

        // 3. Users (Admin only)
        var userResults = new List<SearchResultItem>();
        if (request.Role == "Admin")
        {
            userResults = await _userRepository.ExecuteQueryAsync(
                _userRepository.GetQueryable()
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
                }), cancellationToken);
        }

        var allResults = tournamentResults.Concat(teamResults).Concat(userResults).ToList();
        var totalCount = allResults.Count;
        var pagedResults = allResults
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<SearchResultItem>(pagedResults, totalCount, request.Page, pageSize);
    }
}
