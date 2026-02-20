using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.ActivateGovernorate;

public class ActivateGovernorateCommandHandler : IRequestHandler<ActivateGovernorateCommand, Unit>
{
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ActivateGovernorateCommandHandler> _logger;

    public ActivateGovernorateCommandHandler(
        IRepository<Governorate> governorateRepo,
        IDistributedCache cache,
        ILogger<ActivateGovernorateCommandHandler> logger)
    {
        _governorateRepo = governorateRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Unit> Handle(ActivateGovernorateCommand request, CancellationToken ct)
    {
        var entity = await _governorateRepo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.Id);

        entity.IsActive = true;
        await _governorateRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateGovernorateCacheAsync(_cache, _logger, ct);

        return Unit.Value;
    }
}
