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
        try
        {
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            _logger.LogInformation("SMTP connected. Authenticating as {SenderEmail}...", _emailSettings.SenderEmail);

            await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);
            _logger.LogInformation("SMTP authenticated. Sending email...");

            await smtp.SendAsync(email);
            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
        }
        catch (MailKit.Security.AuthenticationException authEx)
        {
            // Lỗi xác thực Gmail: sai password hoặc Google block IP của cloud server
            _logger.LogError(authEx,
                "SMTP AUTHENTICATION FAILED for {SenderEmail}. " +
                "If deployed on cloud (Render/AWS), Google may be blocking the server IP. " +
                "Check: 1) App Password correct? 2) 'Less secure app' or 2FA App Password?",
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
                "Unexpected error sending email to {ToEmail}. " +
                "Server={SmtpServer}, Port={SmtpPort}",
                toEmail, _emailSettings.SmtpServer, _emailSettings.SmtpPort);
            throw;
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}
