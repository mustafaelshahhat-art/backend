using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Commands.UpdateArea;

public record UpdateAreaCommand(Guid Id, UpdateLocationRequest Request) : IRequest<AreaAdminDto>;
