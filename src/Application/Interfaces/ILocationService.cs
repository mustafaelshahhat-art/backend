using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.DTOs.Locations;

namespace Application.Interfaces;

public interface ILocationService
{
    // ── Public (dropdown) ──
    Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CityDto>> GetCitiesByGovernorateAsync(Guid governorateId, CancellationToken ct = default);
    Task<IReadOnlyList<AreaDto>> GetAreasByCityAsync(Guid cityId, CancellationToken ct = default);

    // ── Validation ──
    Task<bool> ValidateLocationHierarchyAsync(Guid? governorateId, Guid? cityId, Guid? areaId, CancellationToken ct = default);

    // ── Admin: Governorates ──
    Task<PagedResult<GovernorateAdminDto>> GetGovernoratesPagedAsync(int page, int pageSize, string? search = null, bool? isActive = null, CancellationToken ct = default);
    Task<GovernorateAdminDto> CreateGovernorateAsync(CreateGovernorateRequest request, CancellationToken ct = default);
    Task<GovernorateAdminDto> UpdateGovernorateAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);
    Task ActivateGovernorateAsync(Guid id, CancellationToken ct = default);
    Task DeactivateGovernorateAsync(Guid id, CancellationToken ct = default);

    // ── Admin: Cities ──
    Task<PagedResult<CityAdminDto>> GetCitiesPagedAsync(int page, int pageSize, Guid? governorateId = null, string? search = null, bool? isActive = null, CancellationToken ct = default);
    Task<CityAdminDto> CreateCityAsync(CreateCityRequest request, CancellationToken ct = default);
    Task<CityAdminDto> UpdateCityAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);
    Task ActivateCityAsync(Guid id, CancellationToken ct = default);
    Task DeactivateCityAsync(Guid id, CancellationToken ct = default);

    // ── Admin: Areas ──
    Task<PagedResult<AreaAdminDto>> GetAreasPagedAsync(int page, int pageSize, Guid? cityId = null, string? search = null, bool? isActive = null, CancellationToken ct = default);
    Task<AreaAdminDto> CreateAreaAsync(CreateAreaRequest request, CancellationToken ct = default);
    Task<AreaAdminDto> UpdateAreaAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);
    Task ActivateAreaAsync(Guid id, CancellationToken ct = default);
    Task DeactivateAreaAsync(Guid id, CancellationToken ct = default);
}
