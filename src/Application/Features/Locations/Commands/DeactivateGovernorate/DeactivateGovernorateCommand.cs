using MediatR;

namespace Application.Features.Locations.Commands.DeactivateGovernorate;

public record DeactivateGovernorateCommand(Guid Id) : IRequest<Unit>;
