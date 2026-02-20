using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Queries.GetGovernorates;

public record GetGovernoratesQuery() : IRequest<IReadOnlyList<GovernorateDto>>;
