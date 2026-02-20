using Application.DTOs.Settings;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace Application.Features.SystemSettings.Commands.UpdateSettings;

public class UpdateSettingsCommandHandler : IRequestHandler<UpdateSettingsCommand, SystemSettingsDto>
{
    private readonly IRepository<SystemSetting> _settingsRepository;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "SystemSettings_Global";

    public UpdateSettingsCommandHandler(IRepository<SystemSetting> settingsRepository, IDistributedCache cache)
    {
        _settingsRepository = settingsRepository;
        _cache = cache;
    }

    public async Task<SystemSettingsDto> Handle(UpdateSettingsCommand request, CancellationToken ct)
    {
        var result = await _settingsRepository.GetPagedAsync(1, 10, null, q => q.OrderBy(s => s.CreatedAt), ct);
        var settings = result.Items.FirstOrDefault();

        if (settings == null)
        {
            settings = new SystemSetting
            {
                AllowTeamCreation = true,
                MaintenanceMode = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _settingsRepository.AddAsync(settings, ct);
        }

        settings.AllowTeamCreation = request.Settings.AllowTeamCreation;
        settings.MaintenanceMode = request.Settings.MaintenanceMode;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = request.AdminId;

        await _settingsRepository.UpdateAsync(settings, ct);
        await _cache.RemoveAsync(CacheKey, ct);

        return new SystemSettingsDto
        {
            AllowTeamCreation = settings.AllowTeamCreation,
            MaintenanceMode = settings.MaintenanceMode,
            UpdatedAt = settings.UpdatedAt
        };
    }
}
