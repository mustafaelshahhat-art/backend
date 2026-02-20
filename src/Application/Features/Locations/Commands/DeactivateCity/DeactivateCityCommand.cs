using MediatR;

namespace Application.Features.Locations.Commands.DeactivateCity;

public record DeactivateCityCommand(Guid Id) : IRequest<Unit>;
