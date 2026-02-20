using Application.Common.Models;
using Application.DTOs.Tournaments;
using Application.Features.Tournaments;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetTournamentsPaged;

public class GetTournamentsPagedQueryHandler : IRequestHandler<GetTournamentsPagedQuery, PagedResult<TournamentDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IMapper _mapper;

    public GetTournamentsPagedQueryHandler(IRepository<Tournament> tournamentRepository, IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<TournamentDto>> Handle(GetTournamentsPagedQuery request, CancellationToken ct)
    {
        var page = request.Page;
        var pageSize = request.PageSize;

        if (pageSize > 100) pageSize = 100;

        // Role-based filtering (moved from controller)
        Guid? creatorId = null;
        bool includeDrafts = false;

        if (request.UserRole == UserRole.TournamentCreator.ToString())
        {
            creatorId = request.UserId;
            includeDrafts = true;
        }
        else if (request.UserRole == UserRole.Admin.ToString())
        {
            includeDrafts = true;
        }

        var query = _tournamentRepository.GetQueryable();

        if (creatorId.HasValue)
        {
            query = query.Where(t => t.CreatorUserId == creatorId.Value);
        }
        else if (!includeDrafts)
        {
            query = query.Where(t => t.Status != TournamentStatus.Draft);
        }

        var totalCount = await _tournamentRepository.ExecuteCountAsync(query, ct);
        var items = await _tournamentRepository.ExecuteQueryAsync(query
            .OrderByDescending(t => t.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                Tournament = t,
                WinnerTeamName = t.WinnerTeam != null ? t.WinnerTeam.Name : null,
                TotalMatches = t.Matches.Count(),
                FinishedMatches = t.Matches.Count(m => m.Status == MatchStatus.Finished),
                TotalRegs = t.Registrations.Count(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn),
                ApprovedRegs = t.Registrations.Count(r => r.Status == RegistrationStatus.Approved)
            }), ct);

        var dtos = new List<TournamentDto>();
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var dto = _mapper.Map<TournamentDto>(item.Tournament);
            dto.WinnerTeamName = item.WinnerTeamName;

            // List view does not need individual registrations; counts are already computed.
            dto.Registrations = new List<TeamRegistrationDto>();

            dto.RequiresAdminIntervention = TournamentHelper.CheckInterventionRequired(item.Tournament,
                item.TotalMatches,
                item.FinishedMatches,
                item.TotalRegs,
                item.ApprovedRegs,
                now);

            dtos.Add(dto);
        }

        return new PagedResult<TournamentDto>(dtos, totalCount, page, pageSize);
    }
}
