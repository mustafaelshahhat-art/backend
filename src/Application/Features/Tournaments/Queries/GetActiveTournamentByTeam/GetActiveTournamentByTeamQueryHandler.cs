using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetActiveTournamentByTeam;

public class GetActiveTournamentByTeamQueryHandler : IRequestHandler<GetActiveTournamentByTeamQuery, TournamentDto?>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;

    public GetActiveTournamentByTeamQueryHandler(IRepository<TeamRegistration> registrationRepository)
        => _registrationRepository = registrationRepository;

    public async Task<TournamentDto?> Handle(GetActiveTournamentByTeamQuery request, CancellationToken cancellationToken)
    {
        var projected = await _registrationRepository.ExecuteFirstOrDefaultAsync(
            _registrationRepository.GetQueryable()
            .Where(r => r.TeamId == request.TeamId &&
                        r.Status == RegistrationStatus.Approved &&
                        r.Tournament!.Status == TournamentStatus.Active)
            .Select(r => new TournamentDto
            {
                Id = r.Tournament!.Id,
                Name = r.Tournament.Name,
                Status = r.Tournament.Status.ToString(),
                StartDate = r.Tournament.StartDate,
                EndDate = r.Tournament.EndDate,
                ImageUrl = r.Tournament.ImageUrl,
                Format = r.Tournament.Format.ToString(),
                MaxTeams = r.Tournament.MaxTeams,
                CurrentTeams = r.Tournament.CurrentTeams,
                Location = r.Tournament.Location ?? string.Empty,
                CreatorUserId = r.Tournament.CreatorUserId
            }), cancellationToken);

        return projected;
    }
}
