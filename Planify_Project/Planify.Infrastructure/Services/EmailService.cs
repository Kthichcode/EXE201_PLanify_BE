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
        var senderAddress = new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail);

        var email = new MimeMessage();
        email.From.Add(senderAddress);   // hiển thị tên "Planify System" trong hộp thư đến
        email.Sender = senderAddress;    // xác nhận người gửi thực tế
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = body };
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            _logger.LogInformation("[Email] Đang kết nối tới {Server}:{Port}...", _emailSettings.SmtpServer, _emailSettings.SmtpPort);
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.Auto);
            var smtpLogin = string.IsNullOrEmpty(_emailSettings.Login)
                ? _emailSettings.SenderEmail
                : _emailSettings.Login;

            await smtp.AuthenticateAsync(smtpLogin, _emailSettings.Password);
            _logger.LogInformation("[Email] Xác thực thành công (login: {Login}). Đang gửi email...", smtpLogin);

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

