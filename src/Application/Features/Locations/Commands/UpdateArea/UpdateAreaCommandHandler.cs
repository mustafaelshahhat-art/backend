using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.UpdateArea;

public class UpdateAreaCommandHandler : IRequestHandler<UpdateAreaCommand, AreaAdminDto>
{
    private readonly IRepository<Area> _areaRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UpdateAreaCommandHandler> _logger;

    public UpdateAreaCommandHandler(
        IRepository<Area> areaRepo,
        IDistributedCache cache,
        ILogger<UpdateAreaCommandHandler> logger)
    {
        _areaRepo = areaRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AreaAdminDto> Handle(UpdateAreaCommand command, CancellationToken ct)
    {
        var id = command.Id;
        var request = command.Request;

        var entity = await _areaRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u0646\u0637\u0642\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", id);

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var dup = await _areaRepo.AnyAsync(
                a => a.Id != id && a.CityId == entity.CityId && a.NameAr == request.NameAr, ct);
            if (dup)
                throw new ConflictException("\u0645\u0646\u0637\u0642\u0629 \u0628\u0647\u0630\u0627 \u0627\u0644\u0627\u0633\u0645 \u0645\u0648\u062c\u0648\u062f\u0629 \u0628\u0627\u0644\u0641\u0639\u0644 \u0641\u064a \u0647\u0630\u0647 \u0627\u0644\u0645\u062f\u064a\u0646\u0629.");
            entity.NameAr = request.NameAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NameEn))
            entity.NameEn = request.NameEn.Trim();

        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;

        await _areaRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateAreaCacheAsync(_cache, _logger, entity.CityId, ct);

        return await _areaRepo.ExecuteFirstAsync(
            _areaRepo.GetQueryable().Where(a => a.Id == id)
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
            }), ct);
    }
}
