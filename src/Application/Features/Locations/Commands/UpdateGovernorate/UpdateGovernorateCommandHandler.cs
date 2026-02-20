using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.UpdateGovernorate;

public class UpdateGovernorateCommandHandler : IRequestHandler<UpdateGovernorateCommand, GovernorateAdminDto>
{
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UpdateGovernorateCommandHandler> _logger;

    public UpdateGovernorateCommandHandler(
        IRepository<Governorate> governorateRepo,
        IDistributedCache cache,
        ILogger<UpdateGovernorateCommandHandler> logger)
    {
        _governorateRepo = governorateRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GovernorateAdminDto> Handle(UpdateGovernorateCommand command, CancellationToken ct)
    {
        var id = command.Id;
        var request = command.Request;

        var entity = await _governorateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", id);

        if (!string.IsNullOrWhiteSpace(request.NameAr))
        {
            var dup = await _governorateRepo.AnyAsync(g => g.Id != id && g.NameAr == request.NameAr, ct);
            if (dup)
                throw new ConflictException("\u0645\u062d\u0627\u0641\u0638\u0629 \u0628\u0647\u0630\u0627 \u0627\u0644\u0627\u0633\u0645 \u0627\u0644\u0639\u0631\u0628\u064a \u0645\u0648\u062c\u0648\u062f\u0629 \u0628\u0627\u0644\u0641\u0639\u0644.");
            entity.NameAr = request.NameAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NameEn))
        {
            var dup = await _governorateRepo.AnyAsync(g => g.Id != id && g.NameEn == request.NameEn, ct);
            if (dup)
                throw new ConflictException("A governorate with this English name already exists.");
            entity.NameEn = request.NameEn.Trim();
        }

        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;

        await _governorateRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateGovernorateCacheAsync(_cache, _logger, ct);

        return await _governorateRepo.ExecuteFirstAsync(
            _governorateRepo.GetQueryable().Where(g => g.Id == id)
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
            }), ct);
    }
}
