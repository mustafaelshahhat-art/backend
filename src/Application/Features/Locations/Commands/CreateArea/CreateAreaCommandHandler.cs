using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.CreateArea;

public class CreateAreaCommandHandler : IRequestHandler<CreateAreaCommand, AreaAdminDto>
{
    private readonly IRepository<Area> _areaRepo;
    private readonly IRepository<City> _cityRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CreateAreaCommandHandler> _logger;

    public CreateAreaCommandHandler(
        IRepository<Area> areaRepo,
        IRepository<City> cityRepo,
        IDistributedCache cache,
        ILogger<CreateAreaCommandHandler> logger)
    {
        _areaRepo = areaRepo;
        _cityRepo = cityRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AreaAdminDto> Handle(CreateAreaCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var cityExists = await _cityRepo.AnyAsync(c => c.Id == request.CityId, ct);
        if (!cityExists)
            throw new NotFoundException("\u0627\u0644\u0645\u062f\u064a\u0646\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.CityId);

        var exists = await _areaRepo.AnyAsync(
            a => a.CityId == request.CityId && a.NameAr == request.NameAr, ct);
        if (exists)
            throw new ConflictException("\u0645\u0646\u0637\u0642\u0629 \u0628\u0647\u0630\u0627 \u0627\u0644\u0627\u0633\u0645 \u0645\u0648\u062c\u0648\u062f\u0629 \u0628\u0627\u0644\u0641\u0639\u0644 \u0641\u064a \u0647\u0630\u0647 \u0627\u0644\u0645\u062f\u064a\u0646\u0629.");

        var entity = new Area
        {
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            CityId = request.CityId,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _areaRepo.AddAsync(entity, ct);
        await LocationCacheHelper.InvalidateAreaCacheAsync(_cache, _logger, request.CityId, ct);

        var cityName = await _cityRepo.ExecuteFirstAsync(
            _cityRepo.GetQueryable()
            .Where(c => c.Id == request.CityId)
            .Select(c => c.NameAr), ct);

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
}
