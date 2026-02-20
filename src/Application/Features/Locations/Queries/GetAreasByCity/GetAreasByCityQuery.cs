using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Queries.GetAreasByCity;

public record GetAreasByCityQuery(Guid CityId) : IRequest<IReadOnlyList<AreaDto>>;
