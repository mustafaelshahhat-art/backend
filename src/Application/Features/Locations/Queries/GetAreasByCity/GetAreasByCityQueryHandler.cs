using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Application.Features.Locations.Queries.GetAreasByCity;

public class GetAreasByCityQueryHandler : IRequestHandler<GetAreasByCityQuery, IReadOnlyList<AreaDto>>
{
    private readonly IRepository<Area> _areaRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetAreasByCityQueryHandler> _logger;

    public GetAreasByCityQueryHandler(
        IRepository<Area> areaRepo,
        IDistributedCache cache,
        ILogger<GetAreasByCityQueryHandler> logger)
    {
        _areaRepo = areaRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AreaDto>> Handle(GetAreasByCityQuery request, CancellationToken ct)
    {
        var cacheKey = LocationCacheHelper.CacheKeyAreas + request.CityId;

        var cached = await LocationCacheHelper.GetFromCacheAsync<List<AreaDto>>(_cache, _logger, cacheKey, ct);
        if (cached is not null) return cached;

        var items = await _areaRepo.ExecuteQueryAsync(
            _areaRepo.GetQueryable()
            .Where(a => a.CityId == request.CityId && a.IsActive)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.NameAr)
            .Select(a => new AreaDto { Id = a.Id, NameAr = a.NameAr, NameEn = a.NameEn, CityId = a.CityId })
            .Take(500), ct);

        await LocationCacheHelper.SetCacheAsync(
            _cache, _logger, cacheKey, items, LocationCacheHelper.CacheDuration, ct);

        return items;
    }
}
