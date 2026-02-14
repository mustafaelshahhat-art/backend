using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);
}
