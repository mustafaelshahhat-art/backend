using System;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(Guid userId, string type);
    Task<bool> VerifyOtpAsync(Guid userId, string otp, string type);
}
