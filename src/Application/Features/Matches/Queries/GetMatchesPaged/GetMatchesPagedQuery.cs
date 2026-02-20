using Application.Common.Models;
using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Queries.GetMatchesPaged;

public record GetMatchesPagedQuery(int Page, int PageSize, Guid? CreatorId = null, string? Status = null, Guid? TeamId = null) : IRequest<PagedResult<MatchDto>>;
