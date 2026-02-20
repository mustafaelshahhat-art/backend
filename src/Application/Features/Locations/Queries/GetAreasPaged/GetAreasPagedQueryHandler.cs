using Application.Common.Models;
using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Locations.Queries.GetAreasPaged;

public class GetAreasPagedQueryHandler : IRequestHandler<GetAreasPagedQuery, PagedResult<AreaAdminDto>>
{
    private readonly IRepository<Area> _areaRepo;

    public GetAreasPagedQueryHandler(IRepository<Area> areaRepo)
    {
        _areaRepo = areaRepo;
    }

    public async Task<PagedResult<AreaAdminDto>> Handle(GetAreasPagedQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var query = _areaRepo.GetQueryable();

        if (request.CityId.HasValue)
            query = query.Where(a => a.CityId == request.CityId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(a => a.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(a => a.NameAr.Contains(request.Search) || a.NameEn.Contains(request.Search));

        var totalCount = await _areaRepo.ExecuteCountAsync(query, ct);

        var items = await _areaRepo.ExecuteQueryAsync(query.OrderBy(a => a.SortOrder).ThenBy(a => a.NameAr)
            .Skip((request.Page - 1) * pageSize).Take(pageSize)
            .Select(a => new AreaAdminDto
            {
                Id = a.Id,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                CityId = a.CityId,
                CityNameAr = a.City.NameAr,
                IsActive = a.IsActive,
                SortOrder = a.SortOrder,
                UserCount = a.Users.Count,
                CreatedAt = a.CreatedAt
            }), ct);

        return new PagedResult<AreaAdminDto>(items, totalCount, request.Page, pageSize);
    }
}
