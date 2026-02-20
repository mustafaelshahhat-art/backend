using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.ActivateCity;

public class ActivateCityCommandHandler : IRequestHandler<ActivateCityCommand, Unit>
{
    private readonly IRepository<City> _cityRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ActivateCityCommandHandler> _logger;

    public ActivateCityCommandHandler(
        IRepository<City> cityRepo,
        IDistributedCache cache,
        ILogger<ActivateCityCommandHandler> logger)
    {
        _cityRepo = cityRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Unit> Handle(ActivateCityCommand request, CancellationToken ct)
    {
        var entity = await _cityRepo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u062f\u064a\u0646\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.Id);

        entity.IsActive = true;
        await _cityRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateCityCacheAsync(_cache, _logger, entity.GovernorateId, ct);

        return Unit.Value;
    }
}
