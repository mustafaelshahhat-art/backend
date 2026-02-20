using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Locations.Commands.DeactivateGovernorate;

public class DeactivateGovernorateCommandHandler : IRequestHandler<DeactivateGovernorateCommand, Unit>
{
    private readonly IRepository<Governorate> _governorateRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DeactivateGovernorateCommandHandler> _logger;

    public DeactivateGovernorateCommandHandler(
        IRepository<Governorate> governorateRepo,
        IRepository<User> userRepo,
        IDistributedCache cache,
        ILogger<DeactivateGovernorateCommandHandler> logger)
    {
        _governorateRepo = governorateRepo;
        _userRepo = userRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivateGovernorateCommand request, CancellationToken ct)
    {
        var entity = await _governorateRepo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("\u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629 \u063a\u064a\u0631 \u0645\u0648\u062c\u0648\u062f\u0629.", request.Id);

        var hasUsers = await _userRepo.AnyAsync(u => u.GovernorateId == request.Id, ct);
        if (hasUsers)
            throw new BadRequestException("\u0644\u0627 \u064a\u0645\u0643\u0646 \u062a\u0639\u0637\u064a\u0644 \u0627\u0644\u0645\u062d\u0627\u0641\u0638\u0629 \u0644\u0648\u062c\u0648\u062f \u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0645\u0631\u062a\u0628\u0637\u064a\u0646 \u0628\u0647\u0627. \u0642\u0645 \u0628\u0646\u0642\u0644 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0623\u0648\u0644\u0627\u064b.");

        entity.IsActive = false;
        await _governorateRepo.UpdateAsync(entity, ct);
        await LocationCacheHelper.InvalidateGovernorateCacheAsync(_cache, _logger, ct);

        return Unit.Value;
    }
}
