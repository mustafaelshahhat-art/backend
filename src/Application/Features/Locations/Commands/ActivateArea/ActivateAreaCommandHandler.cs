using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.ActivateArea;

public class ActivateAreaCommandHandler : IRequestHandler<ActivateAreaCommand, Unit>
{
    private readonly IRepository<Area> _areaRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ActivateAreaCommandHandler> _logger;

    public ActivateAreaCommandHandler(
        IRepository<Area> areaRepo,
        IDistributedCache cache,
        ILogger<ActivateAreaCommandHandler> logger)
    {
        _areaRepo = areaRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Unit> Handle(ActivateAreaCommand request, CancellationToken ct)
    {
        var entity = await _areaRepo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u0646\u0637\u0642\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.Id);

        entity.IsActive = true;
        await _areaRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateAreaCacheAsync(_cache, _logger, entity.CityId, ct);

        return Unit.Value;
    }
}
