using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Commands.CreateCity;

public record CreateCityCommand(CreateCityRequest Request) : IRequest<CityAdminDto>;
