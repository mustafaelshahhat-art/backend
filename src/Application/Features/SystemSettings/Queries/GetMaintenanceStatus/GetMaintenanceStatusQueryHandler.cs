using System.Text.Json;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace Application.Features.SystemSettings.Queries.GetMaintenanceStatus;

public class GetMaintenanceStatusQueryHandler : IRequestHandler<GetMaintenanceStatusQuery, bool>
{
    private readonly IRepository<SystemSetting> _settingsRepository;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "SystemSettings_Global";

    public GetMaintenanceStatusQueryHandler(IRepository<SystemSetting> settingsRepository, IDistributedCache cache)
    {
        _settingsRepository = settingsRepository;
        _cache = cache;
    }

    public async Task<bool> Handle(GetMaintenanceStatusQuery request, CancellationToken ct)
    {
        var cached = await _cache.GetStringAsync(CacheKey, ct);
        if (cached != null)
        {
            var settings = JsonSerializer.Deserialize<SystemSetting>(cached);
            if (settings != null) return settings.MaintenanceMode;
        }

        var result = await _settingsRepository.GetPagedAsync(1, 10, null, q => q.OrderBy(s => s.CreatedAt), ct);
        var dbSettings = result.Items.FirstOrDefault();
        return dbSettings?.MaintenanceMode ?? false;
    }
}
