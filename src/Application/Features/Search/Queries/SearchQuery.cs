using Application.Common.Models;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Search.Queries;

public record SearchQuery(string Query, int Page, int PageSize, string? UserId, string Role) : IRequest<PagedResult<SearchResultItem>>;
