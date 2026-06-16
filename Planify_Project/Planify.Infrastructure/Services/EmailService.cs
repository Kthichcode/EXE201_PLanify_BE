using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planify.Application.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Services;

/// <summary>
/// Gửi email qua Resend HTTP API (https://resend.com).
/// Dùng port 443 HTTPS — không bị block bởi Render/cloud hosting.
/// Thay thế MailKit SMTP vì Render block tất cả outbound SMTP port (587, 465).
/// Free tier: 3000 emails/tháng.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(
        IOptions<EmailSettings> emailSettings,
        ILogger<EmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        _logger.LogInformation("Sending email to {ToEmail} via Resend HTTP API", toEmail);

        var apiKey = _emailSettings.ResendApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("ResendApiKey is not configured. Set it in appsettings.json or Render Environment Variables.");
            throw new InvalidOperationException("ResendApiKey is not configured.");
        }

        var payload = new
        {
            from = $"{_emailSettings.SenderName} <{_emailSettings.SenderEmail}>",
            to = new[] { toEmail },
            subject = subject,
            html = body
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = _httpClientFactory.CreateClient("Resend");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var response = await client.PostAsync("https://api.resend.com/emails", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {ToEmail}. Response: {Response}",
                    toEmail, responseBody);
            }
            else
            {
                _logger.LogError("Resend API returned error {StatusCode} for {ToEmail}. Body: {Body}",
                    response.StatusCode, toEmail, responseBody);
                throw new HttpRequestException(
                    $"Resend API error {response.StatusCode}: {responseBody}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Resend API for {ToEmail}", toEmail);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Resend API call timed out for {ToEmail}", toEmail);
            throw;
        }
    }
}
