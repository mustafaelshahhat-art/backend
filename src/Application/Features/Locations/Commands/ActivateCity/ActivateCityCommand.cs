using MediatR;

namespace Application.Features.Locations.Commands.ActivateCity;

public record ActivateCityCommand(Guid Id) : IRequest<Unit>;
