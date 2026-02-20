using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Commands.UpdateCity;

public record UpdateCityCommand(Guid Id, UpdateLocationRequest Request) : IRequest<CityAdminDto>;
