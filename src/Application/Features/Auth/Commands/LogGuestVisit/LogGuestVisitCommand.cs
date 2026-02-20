using MediatR;

namespace Application.Features.Auth.Commands.LogGuestVisit;

public record LogGuestVisitCommand() : IRequest<Unit>;
