using MediatR;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Features.Admin.Commands.RetryOutboxMessage;

public record RetryOutboxMessageCommand(Guid MessageId) : IRequest<bool>;

public class RetryOutboxMessageCommandHandler : IRequestHandler<RetryOutboxMessageCommand, bool>
{
    private readonly IRepository<OutboxMessage> _outboxRepository;

    public RetryOutboxMessageCommandHandler(IRepository<OutboxMessage> outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public async Task<bool> Handle(RetryOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _outboxRepository.GetByIdAsync(request.MessageId, cancellationToken);
        if (message == null || message.Status != OutboxMessageStatus.DeadLetter)
        {
            return false;
        }

        message.Status = OutboxMessageStatus.Pending;
        message.RetryCount = 0;
        message.Error = null;
        message.DeadLetterReason = null;
        message.ScheduledAt = DateTime.UtcNow;
        message.UpdatedAt = DateTime.UtcNow;

        await _outboxRepository.UpdateAsync(message, cancellationToken);
        return true;
    }
}
