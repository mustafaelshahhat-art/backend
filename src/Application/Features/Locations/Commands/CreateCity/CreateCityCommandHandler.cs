using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.CreateCity;

public class CreateCityCommandHandler : IRequestHandler<CreateCityCommand, CityAdminDto>
{
    private readonly IRepository<City> _cityRepo;
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CreateCityCommandHandler> _logger;

    public CreateCityCommandHandler(
        IRepository<City> cityRepo,
        IRepository<Governorate> governorateRepo,
        IDistributedCache cache,
        ILogger<CreateCityCommandHandler> logger)
    {
        _cityRepo = cityRepo;
        _governorateRepo = governorateRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CityAdminDto> Handle(CreateCityCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var govExists = await _governorateRepo.AnyAsync(g => g.Id == request.GovernorateId, ct);
        if (!govExists)
            throw new NotFoundException("\u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.GovernorateId);

        var exists = await _cityRepo.AnyAsync(
            c => c.GovernorateId == request.GovernorateId && c.NameAr == request.NameAr, ct);
        if (exists)
            throw new ConflictException("\u0645\u062f\u064a\u0646\u0629 \u0628\u0647\u0630\u0627 \u0627\u0644\u0627\u0633\u0645 \u0645\u0648\u062c\u0648\u062f\u0629 \u0628\u0627\u0644\u0641\u0639\u0644 \u0641\u064a \u0647\u0630\u0647 \u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629.");

        var entity = new City
        {
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            GovernorateId = request.GovernorateId,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _cityRepo.AddAsync(entity, ct);
        await LocationCacheHelper.InvalidateCityCacheAsync(_cache, _logger, request.GovernorateId, ct);

        var govName = await _governorateRepo.ExecuteFirstAsync(
            _governorateRepo.GetQueryable()
            .Where(g => g.Id == request.GovernorateId)
            .Select(g => g.NameAr), ct);

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
}
