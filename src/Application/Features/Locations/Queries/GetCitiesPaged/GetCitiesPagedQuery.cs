using Application.Common.Models;
using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Queries.GetCitiesPaged;

public record GetCitiesPagedQuery(int Page, int PageSize, Guid? GovernorateId = null, string? Search = null, bool? IsActive = null) : IRequest<PagedResult<CityAdminDto>>;
