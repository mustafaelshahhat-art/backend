using Application.Common.Models;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Queries.GetGroups;

public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, PagedResult<GroupDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;

    public GetGroupsQueryHandler(IRepository<Tournament> tournamentRepository)
        => _tournamentRepository = tournamentRepository;

    public async Task<PagedResult<GroupDto>> Handle(GetGroupsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize > 100 ? 100 : request.PageSize;

        // PERF: Project only the 2 needed fields instead of loading the full Tournament entity.
        // Before: SELECT * FROM Tournaments WHERE Id = @id (~20 columns)
        // After:  SELECT Format, NumberOfGroups FROM Tournaments WHERE Id = @id (2 columns)
        var tournamentData = await _tournamentRepository.ExecuteFirstOrDefaultAsync(
            _tournamentRepository.GetQueryable()
                .Where(t => t.Id == request.TournamentId)
                .Select(t => new { t.Format, t.NumberOfGroups }), cancellationToken);

        if (tournamentData == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        var groups = new List<GroupDto>();

        if (tournamentData.Format == TournamentFormat.GroupsThenKnockout ||
            tournamentData.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
            int count = tournamentData.NumberOfGroups > 0 ? tournamentData.NumberOfGroups : 1;
            for (int i = 1; i <= count; i++)
            {
                groups.Add(new GroupDto { Id = i, Name = $"المجموعة {i}" });
            }
        }
        else if (tournamentData.Format == TournamentFormat.RoundRobin)
        {
            groups.Add(new GroupDto { Id = 1, Name = "الدوري" });
        }

        var totalCount = groups.Count;
        var items = groups
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<GroupDto>(items, totalCount, request.Page, pageSize);
    }
}
