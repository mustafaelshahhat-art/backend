using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        // 1. Robust Credential Retrieval
        var gmailUser = _configuration["EmailSettings:Username"] ?? Environment.GetEnvironmentVariable("GMAIL_USER");
        var gmailPass = _configuration["EmailSettings:Password"] ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");

        if (string.IsNullOrEmpty(gmailUser) || string.IsNullOrEmpty(gmailPass))
        {
            _logger.LogCritical("[EMAIL_ERROR] Missing credentials (Username or Password). Check appsettings or Env Vars.");
            throw new InvalidOperationException("Email credentials are not configured.");
        }

        // Masked logging for debugging production
        _logger.LogInformation("[EMAIL_PROD_DEBUG] Attempting to send email via Gmail SMTP. Account: {User}****", 
            gmailUser.Length > 5 ? gmailUser.Substring(0, 5) : "HIDDEN");

        // 2. UX: Enhanced Sender Identity
        var fromName = "Kora Zone 365 | التحقق من الحساب";
        if (subject.Contains("تعيين")) fromName = "Kora Zone 365 | استعادة الحساب";

        try
        {
            using (var message = new MailMessage())
            {
                message.From = new MailAddress(gmailUser, fromName);
                message.To.Add(new MailAddress(to));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                message.Headers.Add("X-Priority", "1");
                message.Headers.Add("Importance", "High");

                // Gmail SMTP Configuration
                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(gmailUser, gmailPass);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 20000; // 20 seconds internal timeout

                    // Optional short delay to avoid spam filters on high-velocity bursts
                    await Task.Delay(Random.Shared.Next(100, 300), ct);

                    await client.SendMailAsync(message, ct);
                    _logger.LogInformation("[EMAIL_SUCCESS] Message '{Subject}' sent successfully to {Recipient}", subject, to);
                }
            }
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "[EMAIL_SMTP_FAILURE] SMTP specific error sending to {Recipient}. Code: {Code}, Status: {Status}", 
                to, smtpEx.StatusCode, smtpEx.InnerException?.Message);
            throw; // Re-throw for Polly resilience policy to catch
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMAIL_GENERIC_FAILURE] General error sending email to {Recipient}: {Message}", to, ex.Message);
            throw; 
        }
    }
}
