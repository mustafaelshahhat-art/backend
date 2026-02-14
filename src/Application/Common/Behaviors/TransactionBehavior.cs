using MediatR;
using Application.Interfaces;

namespace Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITransactionManager _transactionManager;

    public TransactionBehavior(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Filter out Queries (convention-based)
        if (request.GetType().Name.EndsWith("Query"))
        {
            return await next();
        }

        return await _transactionManager.ExecuteInTransactionAsync(async () => 
        {
            var result = await next();
            // Note: SaveChangesAsync is handled inside TransactionManager's rollback/commit strategy
            // but we ensure the manager is aware it needs to persist.
            return result;
        }, cancellationToken);
    }
}
