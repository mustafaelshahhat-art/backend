using Application.DTOs.Teams;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using System.Linq.Expressions;

namespace Application.Features.Teams.Queries.GetTeamById;

public class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, TeamDto?>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IMapper _mapper;

    public GetTeamByIdQueryHandler(IRepository<Team> teamRepository, IMapper mapper)
    {
        _teamRepository = teamRepository;
        _mapper = mapper;
    }

    public async Task<TeamDto?> Handle(GetTeamByIdQuery request, CancellationToken ct)
    {
        var team = await _teamRepository.GetByIdNoTrackingAsync(request.Id,
            new Expression<Func<Team, object>>[] { t => t.Players, t => t.Statistics! }, ct);
        return team == null ? null : _mapper.Map<TeamDto>(team);
    }
}
