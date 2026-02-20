using Application.Common.Models;
using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.MatchChat.Queries.GetChatHistory;

public record GetChatHistoryQuery(Guid MatchId, int PageSize, int Page) : IRequest<PagedResult<MatchMessageDto>>;
