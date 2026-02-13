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

        try
        {
            await _transactionManager.BeginTransactionAsync();
            
            var response = await next();
            
            await _transactionManager.CommitTransactionAsync();
            
            return response;
        }
        catch (Exception)
        {
            await _transactionManager.RollbackTransactionAsync();
            throw;
        }
    }
}
