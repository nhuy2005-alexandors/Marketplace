using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.Notifications;

// Mock email provider: chỉ log ra console/log sink, không gửi SMTP thật.
// Swappable sau này bằng provider SMTP thật (implement cùng IEmailSender).
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[MockEmail] To: {ToEmail} | Subject: {Subject} | Body: {Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}
