using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.DeactivateCity;

public class DeactivateCityCommandHandler : IRequestHandler<DeactivateCityCommand, Unit>
{
    private readonly IRepository<City> _cityRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DeactivateCityCommandHandler> _logger;

    public DeactivateCityCommandHandler(
        IRepository<City> cityRepo,
        IRepository<User> userRepo,
        IDistributedCache cache,
        ILogger<DeactivateCityCommandHandler> logger)
    {
        _cityRepo = cityRepo;
        _userRepo = userRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivateCityCommand request, CancellationToken ct)
    {
        var entity = await _cityRepo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u062f\u064a\u0646\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.Id);

        var hasUsers = await _userRepo.AnyAsync(u => u.CityId == request.Id, ct);
        if (hasUsers)
            throw new BadRequestException("\u0644\u0627 \u064a\u0645\u0643\u0646 \u062a\u0639\u0637\u064a\u0644 \u0627\u0644\u0645\u062f\u064a\u0646\u0629 \u0644\u0648\u062c\u0648\u062f \u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0645\u0631\u062a\u0628\u0637\u064a\u0646 \u0628\u0647\u0627.");

        entity.IsActive = false;
        await _cityRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateCityCacheAsync(_cache, _logger, entity.GovernorateId, ct);

        return Unit.Value;
    }
}
