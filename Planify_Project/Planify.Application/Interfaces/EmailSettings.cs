namespace Planify.Application.Interfaces;

public class EmailSettings
{
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// API Key từ https://resend.com — dùng thay SMTP vì Render block outbound SMTP ports.
    /// Set trong Render Environment Variables: EmailSettings__ResendApiKey=re_xxxxxx
    /// </summary>
    public string ResendApiKey { get; set; } = string.Empty;
}
