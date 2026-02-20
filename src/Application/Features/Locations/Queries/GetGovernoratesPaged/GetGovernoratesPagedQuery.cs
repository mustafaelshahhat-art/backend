using Application.Common.Models;
using Application.DTOs.Locations;
using MediatR;

namespace Application.Features.Locations.Queries.GetGovernoratesPaged;

public record GetGovernoratesPagedQuery(int Page, int PageSize, string? Search = null, bool? IsActive = null) : IRequest<PagedResult<GovernorateAdminDto>>;
