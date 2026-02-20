using Application.Common.Models;
using Application.DTOs.Teams;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamPlayers;

public class GetTeamPlayersQueryHandler : IRequestHandler<GetTeamPlayersQuery, PagedResult<PlayerDto>>
{
    private readonly IRepository<Player> _playerRepository;
    private readonly IMapper _mapper;

    public GetTeamPlayersQueryHandler(IRepository<Player> playerRepository, IMapper mapper)
    {
        _playerRepository = playerRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<PlayerDto>> Handle(GetTeamPlayersQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var (items, totalCount) = await _playerRepository.GetPagedAsync(
            request.Page, pageSize,
            p => p.TeamId == request.TeamId,
            q => q.OrderBy(p => p.Name), ct);
        var dtos = _mapper.Map<List<PlayerDto>>(items);
        return new PagedResult<PlayerDto>(dtos, totalCount, request.Page, pageSize);
    }
}
