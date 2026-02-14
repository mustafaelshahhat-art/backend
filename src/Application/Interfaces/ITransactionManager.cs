using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ITransactionManager
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}
