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
        _logger.LogInformation("[Email] Bắt đầu gửi email tới {ToEmail} | Subject: {Subject}", toEmail, subject);
        _logger.LogInformation("[Email] SMTP Config → Server: {Server}, Port: {Port}, SenderEmail: {Sender}",
            _emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.SenderEmail);

        var email = new MimeMessage();
        email.Sender = MailboxAddress.Parse(_emailSettings.SenderEmail);
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = body };
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            _logger.LogInformation("[Email] Đang kết nối tới {Server}:{Port}...", _emailSettings.SmtpServer, _emailSettings.SmtpPort);
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            _logger.LogInformation("[Email] Kết nối thành công. Đang xác thực tài khoản {Sender}...", _emailSettings.SenderEmail);

            await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);
            _logger.LogInformation("[Email] Xác thực thành công. Đang gửi email...");

            await smtp.SendAsync(email);
            _logger.LogInformation("[Email] ✅ Gửi email thành công tới {ToEmail}", toEmail);
        }
        catch (MailKit.Net.Smtp.SmtpCommandException ex)
        {
            _logger.LogError(ex, "[Email] ❌ SMTP Command Error (StatusCode: {StatusCode}) khi gửi tới {ToEmail}", ex.StatusCode, toEmail);
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException ex)
        {
            _logger.LogError(ex, "[Email] ❌ SMTP Protocol Error khi gửi tới {ToEmail}", toEmail);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            _logger.LogError(ex, "[Email] ❌ Không thể kết nối tới SMTP server {Server}:{Port} — SocketError: {SocketError}", _emailSettings.SmtpServer, _emailSettings.SmtpPort, ex.SocketErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] ❌ Lỗi không xác định khi gửi email tới {ToEmail} — {ErrorType}: {Message}", toEmail, ex.GetType().Name, ex.Message);
        }
        finally
        {
            await smtp.DisconnectAsync(true);
            _logger.LogInformation("[Email] Đã ngắt kết nối SMTP.");
        }
    }
}
