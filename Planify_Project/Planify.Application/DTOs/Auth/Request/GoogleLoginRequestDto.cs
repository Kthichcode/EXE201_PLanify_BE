namespace Planify.Application.DTOs.Auth.Request;

public class GoogleLoginRequestDto
{
    /// <summary>
    /// ID Token nhận được từ Google Sign-In SDK phía client
    /// </summary>
    public string IdToken { get; set; } = string.Empty;
}
