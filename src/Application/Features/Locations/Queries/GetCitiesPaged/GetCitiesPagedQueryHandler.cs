using Application.Common.Models;
using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Locations.Queries.GetCitiesPaged;

public class GetCitiesPagedQueryHandler : IRequestHandler<GetCitiesPagedQuery, PagedResult<CityAdminDto>>
{
    private readonly IRepository<City> _cityRepo;

    public GetCitiesPagedQueryHandler(IRepository<City> cityRepo)
    {
        _cityRepo = cityRepo;
    }

    public async Task<PagedResult<CityAdminDto>> Handle(GetCitiesPagedQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var query = _cityRepo.GetQueryable();

        if (request.GovernorateId.HasValue)
            query = query.Where(c => c.GovernorateId == request.GovernorateId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(c => c.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(c => c.NameAr.Contains(request.Search) || c.NameEn.Contains(request.Search));

        var totalCount = await _cityRepo.ExecuteCountAsync(query, ct);

        var items = await _cityRepo.ExecuteQueryAsync(query.OrderBy(c => c.SortOrder).ThenBy(c => c.NameAr)
            .Skip((request.Page - 1) * pageSize).Take(pageSize)
            .Select(c => new CityAdminDto
            {
                Id = c.Id,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                GovernorateId = c.GovernorateId,
                GovernorateNameAr = c.Governorate.NameAr,
                IsActive = c.IsActive,
                SortOrder = c.SortOrder,
                AreaCount = c.Areas.Count,
                UserCount = c.Users.Count,
                CreatedAt = c.CreatedAt
            }), ct);

        return new PagedResult<CityAdminDto>(items, totalCount, request.Page, pageSize);
    }
}
