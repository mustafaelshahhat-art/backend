using MediatR;

namespace Application.Features.Locations.Commands.DeactivateArea;

public record DeactivateAreaCommand(Guid Id) : IRequest<Unit>;
