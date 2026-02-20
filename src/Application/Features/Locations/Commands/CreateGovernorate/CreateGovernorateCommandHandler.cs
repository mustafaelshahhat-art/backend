using Application.DTOs.Locations;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.CreateGovernorate;

public class CreateGovernorateCommandHandler : IRequestHandler<CreateGovernorateCommand, GovernorateAdminDto>
{
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CreateGovernorateCommandHandler> _logger;

    public CreateGovernorateCommandHandler(
        IRepository<Governorate> governorateRepo,
        IDistributedCache cache,
        ILogger<CreateGovernorateCommandHandler> logger)
    {
        _governorateRepo = governorateRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GovernorateAdminDto> Handle(CreateGovernorateCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var exists = await _governorateRepo.AnyAsync(
            g => g.NameAr == request.NameAr || g.NameEn == request.NameEn, ct);
        if (exists)
            throw new ConflictException("\u0645\u062d\u0627\u0641\u0638\u0629 \u0628\u0647\u0630\u0627 \u0627\u0644\u0627\u0633\u0645 \u0645\u0648\u062c\u0648\u062f\u0629 \u0628\u0627\u0644\u0641\u0639\u0644.");

        var entity = new Governorate
        {
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _governorateRepo.AddAsync(entity, ct);
        await LocationCacheHelper.InvalidateGovernorateCacheAsync(_cache, _logger, ct);

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
}
