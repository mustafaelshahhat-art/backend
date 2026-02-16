using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

/// <summary>
/// PERF-FIX B4: Channel-based email queue to replace Task.Run fire-and-forget.
/// Emails are enqueued from request threads and processed by a background service,
/// eliminating scope leaks and unobserved exceptions.
/// </summary>
public interface IEmailQueueService
{
    ValueTask EnqueueAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
