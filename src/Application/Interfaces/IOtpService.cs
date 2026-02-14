using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(Guid userId, string type, CancellationToken ct = default);
    Task<bool> VerifyOtpAsync(Guid userId, string otp, string type, CancellationToken ct = default);
}
