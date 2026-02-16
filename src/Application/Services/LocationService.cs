using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.DTOs.Locations;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Services;

public class LocationService : ILocationService
{
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IRepository<City> _cityRepo;
    private readonly IRepository<Area> _areaRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<LocationService> _logger;

    private const string CacheKeyGovernorates = "loc:governorates";
    private const string CacheKeyCities = "loc:cities:";
    private const string CacheKeyAreas = "loc:areas:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public LocationService(
        IRepository<Governorate> governorateRepo,
        IRepository<City> cityRepo,
        IRepository<Area> areaRepo,
        IRepository<User> userRepo,
        IDistributedCache cache,
        ILogger<LocationService> logger)
    {
        _governorateRepo = governorateRepo;
        _cityRepo = cityRepo;
        _areaRepo = areaRepo;
        _userRepo = userRepo;
        _cache = cache;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    //  Public (dropdown) — cached, projection, no tracking
    // ─────────────────────────────────────────────

    public async Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default)
    {
        var cached = await GetFromCacheAsync<List<GovernorateDto>>(CacheKeyGovernorates, ct);
        if (cached is not null) return cached;

        var items = await _governorateRepo.GetQueryable()
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.NameAr)
            .Select(g => new GovernorateDto
            {
                Id = g.Id,
                NameAr = g.NameAr,
                NameEn = g.NameEn
            })
            .ToListAsync(ct);

        await SetCacheAsync(CacheKeyGovernorates, items, CacheDuration, ct);
        return items;
    }

    public async Task<IReadOnlyList<CityDto>> GetCitiesByGovernorateAsync(Guid governorateId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeyCities + governorateId;
        var cached = await GetFromCacheAsync<List<CityDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var items = await _cityRepo.GetQueryable()
            .AsNoTracking()
            .Where(c => c.GovernorateId == governorateId && c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NameAr)
            .Select(c => new CityDto
            {
                Id = c.Id,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                GovernorateId = c.GovernorateId
            })
            .ToListAsync(ct);

        await SetCacheAsync(cacheKey, items, CacheDuration, ct);
        return items;
    }

    public async Task<IReadOnlyList<AreaDto>> GetAreasByCityAsync(Guid cityId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeyAreas + cityId;
        var cached = await GetFromCacheAsync<List<AreaDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var items = await _areaRepo.GetQueryable()
            .AsNoTracking()
            .Where(a => a.CityId == cityId && a.IsActive)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.NameAr)
            .Select(a => new AreaDto
            {
                Id = a.Id,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                CityId = a.CityId
            })
            .ToListAsync(ct);

        await SetCacheAsync(cacheKey, items, CacheDuration, ct);
        return items;
    }

    // ─────────────────────────────────────────────
    //  Validation
    // ─────────────────────────────────────────────

    public async Task<bool> ValidateLocationHierarchyAsync(Guid? governorateId, Guid? cityId, Guid? areaId, CancellationToken ct = default)
    {
        if (governorateId is null && cityId is null && areaId is null)
            return true;

        if (governorateId is null)
            return false;

        var govExists = await _governorateRepo.AnyAsync(g => g.Id == governorateId && g.IsActive, ct);
        if (!govExists) return false;

        if (cityId is null && areaId is not null)
            return false;

        if (cityId is not null)
        {
            var cityBelongs = await _cityRepo.AnyAsync(
                c => c.Id == cityId && c.GovernorateId == governorateId && c.IsActive, ct);
            if (!cityBelongs) return false;
        }

        if (areaId is not null)
        {
            var areaBelongs = await _areaRepo.AnyAsync(
                a => a.Id == areaId && a.CityId == cityId && a.IsActive, ct);
            if (!areaBelongs) return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────
    //  Admin: Governorates
    // ─────────────────────────────────────────────

    public async Task<PagedResult<GovernorateAdminDto>> GetGovernoratesPagedAsync(
        int page, int pageSize, string? search = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _governorateRepo.GetQueryable().AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(g => g.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.NameAr.Contains(search) || g.NameEn.Contains(search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(g => g.SortOrder).ThenBy(g => g.NameAr)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            })
            .ToListAsync(ct);

        return new PagedResult<GovernorateAdminDto>(items, totalCount, page, pageSize);
    }

    public async Task<GovernorateAdminDto> CreateGovernorateAsync(CreateGovernorateRequest request, CancellationToken ct = default)
    {
        var exists = await _governorateRepo.AnyAsync(
            g => g.NameAr == request.NameAr || g.NameEn == request.NameEn, ct);
        if (exists)
            throw new ConflictException("محافظة بهذا الاسم موجودة بالفعل.");

        var entity = new Governorate
        {
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _governorateRepo.AddAsync(entity, ct);
        await InvalidateGovernorateCacheAsync(ct);

        return new GovernorateAdminDto
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            CityCount = 0,
            UserCount = 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<GovernorateAdminDto> UpdateGovernorateAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default)
    {
        var entity = await _governorateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المحافظة غير موجودة.", id);

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var duplicate = await _governorateRepo.AnyAsync(g => g.Id != id && g.NameAr == request.NameAr, ct);
            if (duplicate)
                throw new ConflictException("محافظة بهذا الاسم العربي موجودة بالفعل.");
            entity.NameAr = request.NameAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NameEn))
        {
            var duplicate = await _governorateRepo.AnyAsync(g => g.Id != id && g.NameEn == request.NameEn, ct);
            if (duplicate)
                throw new ConflictException("A governorate with this English name already exists.");
            entity.NameEn = request.NameEn.Trim();
        }

        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;

        await _governorateRepo.UpdateAsync(entity, ct);
        await InvalidateGovernorateCacheAsync(ct);

        return await _governorateRepo.GetQueryable().AsNoTracking()
            .Where(g => g.Id == id)
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
            })
            .FirstAsync(ct);
    }

    public async Task ActivateGovernorateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _governorateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المحافظة غير موجودة.", id);
        entity.IsActive = true;
        await _governorateRepo.UpdateAsync(entity, ct);
        await InvalidateGovernorateCacheAsync(ct);
    }

    public async Task DeactivateGovernorateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _governorateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المحافظة غير موجودة.", id);

        var hasUsers = await _userRepo.AnyAsync(u => u.GovernorateId == id, ct);
        if (hasUsers)
            throw new BadRequestException("لا يمكن تعطيل المحافظة لوجود مستخدمين مرتبطين بها. قم بنقل المستخدمين أولاً.");

        entity.IsActive = false;
        await _governorateRepo.UpdateAsync(entity, ct);
        await InvalidateGovernorateCacheAsync(ct);
    }

    // ─────────────────────────────────────────────
    //  Admin: Cities
    // ─────────────────────────────────────────────

    public async Task<PagedResult<CityAdminDto>> GetCitiesPagedAsync(
        int page, int pageSize, Guid? governorateId = null, string? search = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _cityRepo.GetQueryable().AsNoTracking();

        if (governorateId.HasValue)
            query = query.Where(c => c.GovernorateId == governorateId.Value);

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.NameAr.Contains(search) || c.NameEn.Contains(search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NameAr)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            })
            .ToListAsync(ct);

        return new PagedResult<CityAdminDto>(items, totalCount, page, pageSize);
    }

    public async Task<CityAdminDto> CreateCityAsync(CreateCityRequest request, CancellationToken ct = default)
    {
        var govExists = await _governorateRepo.AnyAsync(g => g.Id == request.GovernorateId, ct);
        if (!govExists)
            throw new NotFoundException("المحافظة غير موجودة.", request.GovernorateId);

        var exists = await _cityRepo.AnyAsync(
            c => c.GovernorateId == request.GovernorateId && c.NameAr == request.NameAr, ct);
        if (exists)
            throw new ConflictException("مدينة بهذا الاسم موجودة بالفعل في هذه المحافظة.");

        var entity = new City
        {
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            GovernorateId = request.GovernorateId,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _cityRepo.AddAsync(entity, ct);
        await InvalidateCityCacheAsync(request.GovernorateId, ct);

        var govName = await _governorateRepo.GetQueryable().AsNoTracking()
            .Where(g => g.Id == request.GovernorateId)
            .Select(g => g.NameAr)
            .FirstAsync(ct);

        return new CityAdminDto
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            GovernorateId = entity.GovernorateId,
            GovernorateNameAr = govName,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            AreaCount = 0,
            UserCount = 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<CityAdminDto> UpdateCityAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default)
    {
        var entity = await _cityRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المدينة غير موجودة.", id);

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var duplicate = await _cityRepo.AnyAsync(
                c => c.Id != id && c.GovernorateId == entity.GovernorateId && c.NameAr == request.NameAr, ct);
            if (duplicate)
                throw new ConflictException("مدينة بهذا الاسم موجودة بالفعل في هذه المحافظة.");
            entity.NameAr = request.NameAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NameEn))
            entity.NameEn = request.NameEn.Trim();

        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;

        await _cityRepo.UpdateAsync(entity, ct);
        await InvalidateCityCacheAsync(entity.GovernorateId, ct);

        return await _cityRepo.GetQueryable().AsNoTracking()
            .Where(c => c.Id == id)
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
            })
            .FirstAsync(ct);
    }

    public async Task ActivateCityAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _cityRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المدينة غير موجودة.", id);
        entity.IsActive = true;
        await _cityRepo.UpdateAsync(entity, ct);
        await InvalidateCityCacheAsync(entity.GovernorateId, ct);
    }

    public async Task DeactivateCityAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _cityRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المدينة غير موجودة.", id);

        var hasUsers = await _userRepo.AnyAsync(u => u.CityId == id, ct);
        if (hasUsers)
            throw new BadRequestException("لا يمكن تعطيل المدينة لوجود مستخدمين مرتبطين بها.");

        entity.IsActive = false;
        await _cityRepo.UpdateAsync(entity, ct);
        await InvalidateCityCacheAsync(entity.GovernorateId, ct);
    }

    // ─────────────────────────────────────────────
    //  Admin: Areas
    // ─────────────────────────────────────────────

    public async Task<PagedResult<AreaAdminDto>> GetAreasPagedAsync(
        int page, int pageSize, Guid? cityId = null, string? search = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _areaRepo.GetQueryable().AsNoTracking();

        if (cityId.HasValue)
            query = query.Where(a => a.CityId == cityId.Value);

        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.NameAr.Contains(search) || a.NameEn.Contains(search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.SortOrder).ThenBy(a => a.NameAr)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            })
            .ToListAsync(ct);

        return new PagedResult<AreaAdminDto>(items, totalCount, page, pageSize);
    }

    public async Task<AreaAdminDto> CreateAreaAsync(CreateAreaRequest request, CancellationToken ct = default)
    {
        var cityExists = await _cityRepo.AnyAsync(c => c.Id == request.CityId, ct);
        if (!cityExists)
            throw new NotFoundException("المدينة غير موجودة.", request.CityId);

        var exists = await _areaRepo.AnyAsync(
            a => a.CityId == request.CityId && a.NameAr == request.NameAr, ct);
        if (exists)
            throw new ConflictException("منطقة بهذا الاسم موجودة بالفعل في هذه المدينة.");

        var entity = new Area
        {
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            CityId = request.CityId,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _areaRepo.AddAsync(entity, ct);
        await InvalidateAreaCacheAsync(request.CityId, ct);

        var cityName = await _cityRepo.GetQueryable().AsNoTracking()
            .Where(c => c.Id == request.CityId)
            .Select(c => c.NameAr)
            .FirstAsync(ct);

        return new AreaAdminDto
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            CityId = entity.CityId,
            CityNameAr = cityName,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            UserCount = 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<AreaAdminDto> UpdateAreaAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default)
    {
        var entity = await _areaRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المنطقة غير موجودة.", id);

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var duplicate = await _areaRepo.AnyAsync(
                a => a.Id != id && a.CityId == entity.CityId && a.NameAr == request.NameAr, ct);
            if (duplicate)
                throw new ConflictException("منطقة بهذا الاسم موجودة بالفعل في هذه المدينة.");
            entity.NameAr = request.NameAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NameEn))
            entity.NameEn = request.NameEn.Trim();

        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;

        await _areaRepo.UpdateAsync(entity, ct);
        await InvalidateAreaCacheAsync(entity.CityId, ct);

        return await _areaRepo.GetQueryable().AsNoTracking()
            .Where(a => a.Id == id)
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
            })
            .FirstAsync(ct);
    }

    public async Task ActivateAreaAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _areaRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المنطقة غير موجودة.", id);
        entity.IsActive = true;
        await _areaRepo.UpdateAsync(entity, ct);
        await InvalidateAreaCacheAsync(entity.CityId, ct);
    }

    public async Task DeactivateAreaAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _areaRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("المنطقة غير موجودة.", id);

        var hasUsers = await _userRepo.AnyAsync(u => u.AreaId == id, ct);
        if (hasUsers)
            throw new BadRequestException("لا يمكن تعطيل المنطقة لوجود مستخدمين مرتبطين بها.");

        entity.IsActive = false;
        await _areaRepo.UpdateAsync(entity, ct);
        await InvalidateAreaCacheAsync(entity.CityId, ct);
    }

    // ─────────────────────────────────────────────
    //  Cache helpers
    // ─────────────────────────────────────────────

    private async Task InvalidateGovernorateCacheAsync(CancellationToken ct)
    {
        try { await _cache.RemoveAsync(CacheKeyGovernorates, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to invalidate governorate cache"); }
    }

    private async Task InvalidateCityCacheAsync(Guid governorateId, CancellationToken ct)
    {
        try { await _cache.RemoveAsync(CacheKeyCities + governorateId, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to invalidate city cache for governorate {GovernorateId}", governorateId); }
    }

    private async Task InvalidateAreaCacheAsync(Guid cityId, CancellationToken ct)
    {
        try { await _cache.RemoveAsync(CacheKeyAreas + cityId, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to invalidate area cache for city {CityId}", cityId); }
    }

    private async Task<T?> GetFromCacheAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes is null || bytes.Length == 0) return null;
            return System.Text.Json.JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {Key}", key);
            return null;
        }
    }

    private async Task SetCacheAsync<T>(string key, T value, TimeSpan duration, CancellationToken ct)
    {
        try
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key {Key}", key);
        }
    }
}
