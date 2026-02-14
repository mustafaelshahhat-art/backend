using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Application.Common; // For hashing
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class OtpService : IOtpService
{
    private readonly IRepository<Otp> _otpRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<OtpService> _logger;

    public OtpService(IRepository<Otp> otpRepository, IPasswordHasher passwordHasher, ILogger<OtpService> logger)
    {
        _otpRepository = otpRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<string> GenerateOtpAsync(Guid userId, string type, CancellationToken ct = default)
    {
        // 1. Invalidate any existing active OTPs for this user and type
        var existingOtps = await _otpRepository.FindAsync(o => o.UserId == userId && o.Type == type && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow, ct);
        foreach (var existing in existingOtps)
        {
            existing.IsUsed = true; // Effectively cancel them
            await _otpRepository.UpdateAsync(existing, ct);
        }

        // 2. Generate 6-digit numeric OTP
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        // 3. Hash OTP
        var hash = _passwordHasher.HashPassword(otp);

        // 4. Save to DB
        var otpEntity = new Otp
        {
            UserId = userId,
            OtpHash = hash,
            Type = type,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            Attempts = 0
        };

        await _otpRepository.AddAsync(otpEntity, ct);

        return otp; // Return clear text OTP to be sent via email
    }

    public async Task<bool> VerifyOtpAsync(Guid userId, string otp, string type, CancellationToken ct = default)
    {
        // 1. Find valid OTP
        var otps = await _otpRepository.FindAsync(o => o.UserId == userId && o.Type == type && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow, ct);
        
        // Sort by CreatedAt desc
        var validOtp = otps.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

        if (validOtp == null)
        {
            _logger.LogWarning($"No valid OTP found for user {userId}");
            return false;
        }

        // 2. Check Attempts
        if (validOtp.Attempts >= 5)
        {
            _logger.LogWarning($"OTP attempts exceeded for user {userId}");
            validOtp.IsUsed = true; // Invalidate
            await _otpRepository.UpdateAsync(validOtp, ct);
            return false;
        }

        // 3. Verify Hash
        bool isValid = _passwordHasher.VerifyPassword(otp, validOtp.OtpHash);

        if (isValid)
        {
            validOtp.IsUsed = true;
            await _otpRepository.UpdateAsync(validOtp, ct);
            return true;
        }
        else
        {
            validOtp.Attempts++;
            await _otpRepository.UpdateAsync(validOtp, ct);
            return false;
        }
    }
}
