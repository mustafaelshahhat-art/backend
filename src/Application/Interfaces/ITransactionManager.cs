using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ITransactionManager
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation);
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
