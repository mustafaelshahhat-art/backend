using Application.Common.Models;
using Application.Contracts.Admin.Responses;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Admin.Queries.GetDeadLetters;

public class GetDeadLettersQueryHandler : IRequestHandler<GetDeadLettersQuery, PagedResult<DeadLetterMessageDto>>
{
    private readonly IOutboxAdminService _outboxAdminService;
    public GetDeadLettersQueryHandler(IOutboxAdminService outboxAdminService) => _outboxAdminService = outboxAdminService;
    public async Task<PagedResult<DeadLetterMessageDto>> Handle(GetDeadLettersQuery request, CancellationToken ct)
    {
        var result = await _outboxAdminService.GetDeadLetterMessagesAsync(request.Page, request.PageSize, ct);
        var messages = result.Messages.Select(m => new DeadLetterMessageDto
        {
            Id = m.Id,
            EventType = m.Type,
            Payload = m.Payload,
            Status = m.Status.ToString(),
            RetryCount = m.RetryCount,
            Error = m.Error,
            CreatedAt = m.OccurredOn,
            ProcessedAt = m.ProcessedOn
        }).ToList();
        return new PagedResult<DeadLetterMessageDto>(messages, result.TotalCount, request.Page, request.PageSize);
    }
}
