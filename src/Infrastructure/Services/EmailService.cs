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
        // Check Configuration first (appsettings), then Environment Variables
        var gmailUser = _configuration["EmailSettings:Username"] ?? Environment.GetEnvironmentVariable("GMAIL_USER");
        var gmailPass = _configuration["EmailSettings:Password"] ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");

        if (string.IsNullOrEmpty(gmailUser) || string.IsNullOrEmpty(gmailPass))
        {
            var error = "CRITICAL: Email credentials missing. Application cannot send emails.";
            _logger.LogCritical(error);
            throw new InvalidOperationException(error);
        }

        // 2. UX: Enhanced Sender Identity for Spam Reputation
        var fromName = "RAMADAN GANA | التحقق من الحساب";
        if (subject.Contains("تعيين")) fromName = "RAMADAN GANA | استعادة الحساب";

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

                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(gmailUser, gmailPass);

                    await Task.Delay(Random.Shared.Next(500, 1000), ct);

                    await client.SendMailAsync(message, ct);
                    _logger.LogInformation($"[EMAIL_SUCCESS] Template sent to {to} | Subject: {subject}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[EMAIL_FAILURE] Critical failure sending to {to}");
            throw; 
        }
    }
}
