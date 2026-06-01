namespace Planify.Application.Interfaces;

/// <summary>
/// Abstraction cho việc sinh và xác thực JWT / Refresh Token.
/// Đặt ở Application để các service trong tầng này có thể inject mà không
/// tạo vòng phụ thuộc với Infrastructure.
/// Implementation: Infrastructure/Services/TokenService.cs
/// </summary>
public interface ITokenService
{
    /// <summary>Sinh JWT access token.</summary>
    (string Token, DateTime Expiration) GenerateAccessToken(
        Guid userId, string email, string fullName, IList<string> roles);

    /// <summary>Sinh Refresh Token ngẫu nhiên (opaque token).</summary>
    (string Token, DateTime Expiration) GenerateRefreshToken();

    /// <summary>Lấy UserId từ JWT đã hết hạn (dùng khi refresh token).</summary>
    string? GetUserIdFromExpiredToken(string token);
}
