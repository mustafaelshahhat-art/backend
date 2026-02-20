using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.UpdateCity;

public class UpdateCityCommandHandler : IRequestHandler<UpdateCityCommand, CityAdminDto>
{
    private readonly IRepository<City> _cityRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UpdateCityCommandHandler> _logger;

    public UpdateCityCommandHandler(
        IRepository<City> cityRepo,
        IDistributedCache cache,
        ILogger<UpdateCityCommandHandler> logger)
    {
        _cityRepo = cityRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CityAdminDto> Handle(UpdateCityCommand command, CancellationToken ct)
    {
        var id = command.Id;
        var request = command.Request;

        var entity = await _cityRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u062f\u064a\u0646\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", id);

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var dup = await _cityRepo.AnyAsync(
                c => c.Id != id && c.GovernorateId == entity.GovernorateId && c.NameAr == request.NameAr, ct);
            if (dup)
                throw new ConflictException("\u0645\u062f\u064a\u0646\u0629 \u0628\u0647\u0630\u0627 \u0627\u0644\u0627\u0633\u0645 \u0645\u0648\u062c\u0648\u062f\u0629 \u0628\u0627\u0644\u0641\u0639\u0644 \u0641\u064a \u0647\u0630\u0647 \u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629.");
            entity.NameAr = request.NameAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NameEn))
            entity.NameEn = request.NameEn.Trim();

        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;

        await _cityRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateCityCacheAsync(_cache, _logger, entity.GovernorateId, ct);

        return await _cityRepo.ExecuteFirstAsync(
            _cityRepo.GetQueryable().Where(c => c.Id == id)
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
    }
}
