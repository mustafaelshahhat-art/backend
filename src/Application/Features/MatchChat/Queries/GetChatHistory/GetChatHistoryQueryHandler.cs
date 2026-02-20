using Application.Common.Models;
using Application.DTOs.Matches;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MatchChat.Queries.GetChatHistory;

public class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, PagedResult<MatchMessageDto>>
{
    private readonly IMatchMessageRepository _messageRepository;

    public GetChatHistoryQueryHandler(IMatchMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<PagedResult<MatchMessageDto>> Handle(GetChatHistoryQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 200);
        var totalCount = await _messageRepository.CountByMatchIdAsync(request.MatchId, ct);
        var messages = await _messageRepository.GetByMatchIdAsync(request.MatchId, pageSize, request.Page, ct);

        var items = messages.Select(m => new MatchMessageDto
        {
            Id = m.Id,
            MatchId = m.MatchId,
            SenderId = m.SenderId,
            SenderName = m.SenderName,
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp
        }).ToList();

        return new PagedResult<MatchMessageDto>(items, totalCount, request.Page, pageSize);
    }
}
