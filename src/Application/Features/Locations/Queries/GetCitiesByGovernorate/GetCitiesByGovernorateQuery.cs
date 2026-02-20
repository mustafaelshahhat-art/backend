using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Queries.GetCitiesByGovernorate;

public record GetCitiesByGovernorateQuery(Guid GovernorateId) : IRequest<IReadOnlyList<CityDto>>;
