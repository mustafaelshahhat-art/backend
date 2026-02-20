using MediatR;

namespace Application.Features.Locations.Commands.ActivateArea;

public record ActivateAreaCommand(Guid Id) : IRequest<Unit>;
