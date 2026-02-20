using Application.DTOs.Tournaments;
using Application.Features.Tournaments;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Tournaments.Queries.GetTournamentById;

public class GetTournamentByIdHandler : IRequestHandler<GetTournamentByIdQuery, TournamentDto?>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IMapper _mapper;

    public GetTournamentByIdHandler(IRepository<Tournament> tournamentRepository, IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
    }

    public async Task<TournamentDto?> Handle(
        GetTournamentByIdQuery request,
        CancellationToken ct)
    {
        var id = request.TournamentId;
        var userId = request.CurrentUserId;
        var userRole = request.CurrentUserRole;

        var item = await _tournamentRepository.ExecuteFirstOrDefaultAsync(
            _tournamentRepository.GetQueryable()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Tournament = t,
                WinnerTeamName = t.WinnerTeam != null ? t.WinnerTeam.Name : null,
                TotalMatches = t.Matches.Count(),
                FinishedMatches = t.Matches.Count(m => m.Status == MatchStatus.Finished),
                TotalRegs = t.Registrations.Count(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn),
                ApprovedRegs = t.Registrations.Count(r => r.Status == RegistrationStatus.Approved),
                Registrations = t.Registrations
                    .Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn)
                    .Select(r => new
                    {
                        Registration = r,
                        TeamName = r.Team != null ? r.Team.Name : string.Empty,
                        CaptainName = r.Team != null && r.Team.Players != null
                            ? r.Team.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty
                            : string.Empty
                    }).ToList()
            }), ct);

        if (item == null) return null;

        // PRIVACY: Privacy filter for Drafts
        if (item.Tournament.Status == TournamentStatus.Draft && item.Tournament.CreatorUserId != userId && userRole != "Admin")
        {
            return null;
        }

        var dto = _mapper.Map<TournamentDto>(item.Tournament);
        dto.WinnerTeamName = item.WinnerTeamName;

        dto.Registrations = item.Registrations.Select(r =>
        {
            var regDto = _mapper.Map<TeamRegistrationDto>(r.Registration);
            regDto.TeamName = r.TeamName;
            regDto.CaptainName = r.CaptainName;
            return regDto;
        }).ToList();

        dto.RequiresAdminIntervention = TournamentHelper.CheckInterventionRequired(item.Tournament,
            item.TotalMatches,
            item.FinishedMatches,
            item.TotalRegs,
            item.ApprovedRegs,
            DateTime.UtcNow);

        return dto;
    }
}
