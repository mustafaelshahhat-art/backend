using Application.Common.Models;
using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Queries.GetAreasPaged;

public record GetAreasPagedQuery(int Page, int PageSize, Guid? CityId = null, string? Search = null, bool? IsActive = null) : IRequest<PagedResult<AreaAdminDto>>;
