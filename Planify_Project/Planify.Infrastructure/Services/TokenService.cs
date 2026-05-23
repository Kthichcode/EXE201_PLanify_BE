using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Planify.Infrastructure.Identity;

namespace Planify.Infrastructure.Services;

public class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Sinh JWT access token</summary>
    public (string token, DateTime expiration) GenerateAccessToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expireMinutes = int.Parse(jwtSettings["ExpireMinutes"] ?? "60");
        var expiration = DateTime.UtcNow.AddMinutes(expireMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("fullName", user.FullName),
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }

    /// <summary>Sinh Refresh Token ngẫu nhiên (opaque token)</summary>
    public (string token, DateTime expiration) GenerateRefreshToken()
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshExpireDays = int.Parse(jwtSettings["RefreshTokenExpireDays"] ?? "7");

        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        return (Convert.ToBase64String(randomBytes), DateTime.UtcNow.AddDays(refreshExpireDays));
    }

    /// <summary>Lấy UserId từ JWT đã hết hạn (dùng khi refresh)</summary>
    public string? GetUserIdFromExpiredToken(string token)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false, // Cho phép token hết hạn
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, tokenValidationParameters, out _);
            return principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        catch
        {
            return null;
        }
    }

    // Giữ tương thích ngược nếu cần (gọi GenerateAccessToken)
    public (string token, DateTime expiration) GenerateToken(ApplicationUser user)
        => GenerateAccessToken(user);
}
