using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Application.Features.Locations.Queries.GetGovernorates;

public class GetGovernoratesQueryHandler : IRequestHandler<GetGovernoratesQuery, IReadOnlyList<GovernorateDto>>
{
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetGovernoratesQueryHandler> _logger;

    public GetGovernoratesQueryHandler(
        IRepository<Governorate> governorateRepo,
        IDistributedCache cache,
        ILogger<GetGovernoratesQueryHandler> logger)
    {
        _governorateRepo = governorateRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GovernorateDto>> Handle(GetGovernoratesQuery request, CancellationToken ct)
    {
        var cached = await LocationCacheHelper.GetFromCacheAsync<List<GovernorateDto>>(
            _cache, _logger, LocationCacheHelper.CacheKeyGovernorates, ct);
        if (cached is not null) return cached;

        var items = await _governorateRepo.ExecuteQueryAsync(
            _governorateRepo.GetQueryable()
            .Where(g => g.IsActive).OrderBy(g => g.SortOrder).ThenBy(g => g.NameAr)
            .Select(g => new GovernorateDto { Id = g.Id, NameAr = g.NameAr, NameEn = g.NameEn })
            .Take(500), ct);

        await LocationCacheHelper.SetCacheAsync(
            _cache, _logger, LocationCacheHelper.CacheKeyGovernorates, items, LocationCacheHelper.CacheDuration, ct);

        return items;
    }
}
