using MediatR;

namespace Application.Features.Locations.Commands.ActivateGovernorate;

public record ActivateGovernorateCommand(Guid Id) : IRequest<Unit>;
