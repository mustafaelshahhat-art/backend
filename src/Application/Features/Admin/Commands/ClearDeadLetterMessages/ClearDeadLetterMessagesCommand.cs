using MediatR;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Features.Admin.Commands.ClearDeadLetterMessages;

public record ClearDeadLetterMessagesCommand() : IRequest<int>;

public class ClearDeadLetterMessagesCommandHandler : IRequestHandler<ClearDeadLetterMessagesCommand, int>
{
    private readonly IRepository<OutboxMessage> _outboxRepository;

    public ClearDeadLetterMessagesCommandHandler(IRepository<OutboxMessage> outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public async Task<int> Handle(ClearDeadLetterMessagesCommand request, CancellationToken cancellationToken)
    {
        var deadLetters = await _outboxRepository.FindAsync(m => m.Status == OutboxMessageStatus.DeadLetter, cancellationToken);
        var count = deadLetters.Count();
        
        await _outboxRepository.DeleteRangeAsync(deadLetters, cancellationToken);
        
        return count;
    }
}
