using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.DeactivateArea;

public class DeactivateAreaCommandHandler : IRequestHandler<DeactivateAreaCommand, Unit>
{
    private readonly IRepository<Area> _areaRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DeactivateAreaCommandHandler> _logger;

    public DeactivateAreaCommandHandler(
        IRepository<Area> areaRepo,
        IRepository<User> userRepo,
        IDistributedCache cache,
        ILogger<DeactivateAreaCommandHandler> logger)
    {
        _areaRepo = areaRepo;
        _userRepo = userRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivateAreaCommand request, CancellationToken ct)
    {
        var entity = await _areaRepo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u0646\u0637\u0642\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.Id);

        var hasUsers = await _userRepo.AnyAsync(u => u.AreaId == request.Id, ct);
        if (hasUsers)
            throw new BadRequestException("\u0644\u0627 \u064a\u0645\u0643\u0646 \u062a\u0639\u0637\u064a\u0644 \u0627\u0644\u0645\u0646\u0637\u0642\u0629 \u0644\u0648\u062c\u0648\u062f \u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0645\u0631\u062a\u0628\u0637\u064a\u0646 \u0628\u0647\u0627.");

        entity.IsActive = false;
        await _areaRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateAreaCacheAsync(_cache, _logger, entity.CityId, ct);

        return Unit.Value;
    }
}
