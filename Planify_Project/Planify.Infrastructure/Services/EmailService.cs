using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Planify.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        _logger.LogInformation("Attempting to send email to {ToEmail} via {SmtpServer}:{SmtpPort}",
            toEmail, _emailSettings.SmtpServer, _emailSettings.SmtpPort);

        var email = new MimeMessage();
        email.Sender = MailboxAddress.Parse(_emailSettings.SenderEmail);
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = body };
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        // Timeout 15 giây — nếu port bị block thì fail nhanh thay vì treo vô thời hạn
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            // SecureSocketOptions.Auto: tự động chọn SSL/TLS phù hợp với port
            // port 465 → SslOnConnect (SSL trực tiếp, không bị block trên cloud)
            // port 587 → StartTls (có thể bị block trên Render/AWS)
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort,
                SecureSocketOptions.Auto, cts.Token);
            _logger.LogInformation("SMTP connected. Authenticating as {SenderEmail}...", _emailSettings.SenderEmail);

            await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password, cts.Token);
            _logger.LogInformation("SMTP authenticated. Sending email...");

            await smtp.SendAsync(email, cancellationToken: cts.Token);
            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "SMTP connection TIMED OUT after 15s connecting to {SmtpServer}:{SmtpPort}. " +
                "Port may be blocked by hosting firewall. Try port 465 in Render Environment Variables.",
                _emailSettings.SmtpServer, _emailSettings.SmtpPort);
            throw;
        }
        catch (MailKit.Security.AuthenticationException authEx)
        {
            _logger.LogError(authEx,
                "SMTP AUTHENTICATION FAILED for {SenderEmail}. " +
                "Check: 1) App Password correct? 2) Gmail 2FA enabled?",
                _emailSettings.SenderEmail);
            throw;
        }
        catch (MailKit.Net.Smtp.SmtpCommandException smtpEx)
        {
            _logger.LogError(smtpEx,
                "SMTP command error sending to {ToEmail}. StatusCode={StatusCode}",
                toEmail, smtpEx.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error sending email to {ToEmail}. Server={SmtpServer}, Port={SmtpPort}",
                toEmail, _emailSettings.SmtpServer, _emailSettings.SmtpPort);
            throw;
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}
