using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Application.Features.Locations.Queries.GetCitiesByGovernorate;

public class GetCitiesByGovernorateQueryHandler : IRequestHandler<GetCitiesByGovernorateQuery, IReadOnlyList<CityDto>>
{
    private readonly IRepository<City> _cityRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetCitiesByGovernorateQueryHandler> _logger;

    public GetCitiesByGovernorateQueryHandler(
        IRepository<City> cityRepo,
        IDistributedCache cache,
        ILogger<GetCitiesByGovernorateQueryHandler> logger)
    {
        _cityRepo = cityRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CityDto>> Handle(GetCitiesByGovernorateQuery request, CancellationToken ct)
    {
        var cacheKey = LocationCacheHelper.CacheKeyCities + request.GovernorateId;

        var cached = await LocationCacheHelper.GetFromCacheAsync<List<CityDto>>(_cache, _logger, cacheKey, ct);
        if (cached is not null) return cached;

        var items = await _cityRepo.ExecuteQueryAsync(
            _cityRepo.GetQueryable()
            .Where(c => c.GovernorateId == request.GovernorateId && c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NameAr)
            .Select(c => new CityDto { Id = c.Id, NameAr = c.NameAr, NameEn = c.NameEn, GovernorateId = c.GovernorateId })
            .Take(500), ct);

        await LocationCacheHelper.SetCacheAsync(
            _cache, _logger, cacheKey, items, LocationCacheHelper.CacheDuration, ct);

        return items;
    }
}
