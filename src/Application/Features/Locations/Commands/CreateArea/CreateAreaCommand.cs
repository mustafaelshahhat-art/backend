using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Commands.CreateArea;

public record CreateAreaCommand(CreateAreaRequest Request) : IRequest<AreaAdminDto>;
