using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class TransactionManager : ITransactionManager
{
    private readonly AppDbContext _context;

    public TransactionManager(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellation);
            try
            {
                var result = await operation();
                await _context.SaveChangesAsync(cancellation);
                await transaction.CommitAsync(cancellation);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellation);
                throw;
            }
        }, ct);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await operation();
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellation);
            try
            {
                await operation();
                await _context.SaveChangesAsync(cancellation);
                await transaction.CommitAsync(cancellation);
            }
            catch
            {
                await transaction.RollbackAsync(cancellation);
                throw;
            }
        }, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
