namespace Planify.Application.Interfaces;

/// <summary>
/// Kết quả xác thực Google ID Token.
/// </summary>
public record GoogleUserInfo(string Email, string FullName);

/// <summary>
/// Abstraction cho việc xác thực Google ID Token.
/// Tách biệt dependency vào Google.Apis.Auth SDK (Infrastructure) khỏi tầng Application.
/// Implementation: Infrastructure/Services/GoogleAuthValidator.cs
/// </summary>
public interface IGoogleAuthValidator
{
    /// <summary>
    /// Xác thực Google ID Token.
    /// </summary>
    /// <returns>Thông tin user nếu hợp lệ; null nếu token không hợp lệ.</returns>
    Task<GoogleUserInfo?> ValidateAsync(string idToken);
}
