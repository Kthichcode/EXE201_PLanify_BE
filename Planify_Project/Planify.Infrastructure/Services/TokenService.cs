using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Planify.Application.Interfaces;

namespace Planify.Infrastructure.Services;

/// <summary>
/// Sinh và xác thực JWT / Refresh Token.
/// Implements ITokenService (Application/Interfaces) — không còn phụ thuộc vào ApplicationUser.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime Expiration) GenerateAccessToken(
        Guid userId, string email, string fullName, IList<string> roles)
    {
        var jwtSettings    = _configuration.GetSection("JwtSettings");
        var key            = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds          = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expireMinutes  = int.Parse(jwtSettings["ExpireMinutes"] ?? "60");
        var expiration     = DateTime.UtcNow.AddMinutes(expireMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("fullName",                    fullName),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             jwtSettings["Issuer"],
            audience:           jwtSettings["Audience"],
            claims:             claims,
            expires:            expiration,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }

    public (string Token, DateTime Expiration) GenerateRefreshToken()
    {
        var jwtSettings         = _configuration.GetSection("JwtSettings");
        var refreshExpireDays   = int.Parse(jwtSettings["RefreshTokenExpireDays"] ?? "7");

        var randomBytes = new byte[64];
        using var rng   = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        return (Convert.ToBase64String(randomBytes), DateTime.UtcNow.AddDays(refreshExpireDays));
    }

    public string? GetUserIdFromExpiredToken(string token)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var parameters  = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = false,   // cho phép token đã hết hạn
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings["Issuer"],
            ValidAudience            = jwtSettings["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out _);
            return principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        catch
        {
            return null;
        }
    }
}
