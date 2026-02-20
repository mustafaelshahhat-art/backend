using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Commands.UpdateGovernorate;

public record UpdateGovernorateCommand(Guid Id, UpdateLocationRequest Request) : IRequest<GovernorateAdminDto>;
