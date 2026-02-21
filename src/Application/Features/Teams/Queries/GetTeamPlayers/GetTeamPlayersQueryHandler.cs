using Application.Common.Models;
using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamPlayers;

public class GetTeamPlayersQueryHandler : IRequestHandler<GetTeamPlayersQuery, PagedResult<PlayerDto>>
{
    private readonly IRepository<Player> _playerRepository;

    public GetTeamPlayersQueryHandler(IRepository<Player> playerRepository)
    {
        _playerRepository = playerRepository;
    }

    public async Task<PagedResult<PlayerDto>> Handle(GetTeamPlayersQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);

        // PERF-FIX: Server-side projection — previously loaded ALL Player columns (SELECT *)
        // then ran AutoMapper reflection. Now projects only needed fields at the DB level.
        var query = _playerRepository.GetQueryable()
            .Where(p => p.TeamId == request.TeamId)
            .OrderBy(p => p.Name);

        var totalCount = await _playerRepository.ExecuteCountAsync(query, ct);

        var projectedQuery = query
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlayerDto
            {
                Id = p.Id,
                Name = p.Name,
                DisplayId = p.DisplayId,
                Number = p.Number,
                Position = p.Position,
                Status = p.Status.ToString(),
                Goals = p.Goals,
                Assists = p.Assists,
                YellowCards = p.YellowCards,
                RedCards = p.RedCards,
                TeamId = p.TeamId,
                UserId = p.UserId,
                TeamRole = p.TeamRole
            });

        var dtos = await _playerRepository.ExecuteQueryAsync(projectedQuery, ct);

        return new PagedResult<PlayerDto>(dtos, totalCount, request.Page, pageSize);
    }
}
