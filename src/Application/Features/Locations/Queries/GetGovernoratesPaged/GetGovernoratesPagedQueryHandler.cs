using Application.Common.Models;
using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Locations.Queries.GetGovernoratesPaged;

public class GetGovernoratesPagedQueryHandler : IRequestHandler<GetGovernoratesPagedQuery, PagedResult<GovernorateAdminDto>>
{
    private readonly IRepository<Governorate> _governorateRepo;

    public GetGovernoratesPagedQueryHandler(IRepository<Governorate> governorateRepo)
    {
        _governorateRepo = governorateRepo;
    }

    public async Task<PagedResult<GovernorateAdminDto>> Handle(GetGovernoratesPagedQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var query = _governorateRepo.GetQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(g => g.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(g => g.NameAr.Contains(request.Search) || g.NameEn.Contains(request.Search));

        var totalCount = await _governorateRepo.ExecuteCountAsync(query, ct);

        var items = await _governorateRepo.ExecuteQueryAsync(query.OrderBy(g => g.SortOrder).ThenBy(g => g.NameAr)
            .Skip((request.Page - 1) * pageSize).Take(pageSize)
            .Select(g => new GovernorateAdminDto
            {
                Id = g.Id,
                NameAr = g.NameAr,
                NameEn = g.NameEn,
                IsActive = g.IsActive,
                SortOrder = g.SortOrder,
                CityCount = g.Cities.Count,
                UserCount = g.Users.Count,
                CreatedAt = g.CreatedAt
            }), ct);

        return new PagedResult<GovernorateAdminDto>(items, totalCount, request.Page, pageSize);
    }
}
