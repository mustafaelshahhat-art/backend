using Application.DTOs.Tournaments;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetRegistrationByTeam;

public class GetRegistrationByTeamQueryHandler : IRequestHandler<GetRegistrationByTeamQuery, TeamRegistrationDto?>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IMapper _mapper;

    public GetRegistrationByTeamQueryHandler(IRepository<TeamRegistration> registrationRepository, IMapper mapper)
    {
        _registrationRepository = registrationRepository;
        _mapper = mapper;
    }

    public async Task<TeamRegistrationDto?> Handle(GetRegistrationByTeamQuery request, CancellationToken cancellationToken)
    {
        var registration = await _registrationRepository.ExecuteFirstOrDefaultAsync(
            _registrationRepository.GetQueryable()
            .Where(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId), cancellationToken);

        return _mapper.Map<TeamRegistrationDto>(registration);
    }
}
