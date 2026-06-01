using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Planify.Application.Interfaces;

namespace Planify.Infrastructure.Services;

/// <summary>
/// Implements IGoogleAuthValidator bằng Google.Apis.Auth SDK.
/// Đọc ClientId từ configuration, không để lộ cấu hình lên tầng Application.
/// </summary>
public class GoogleAuthValidator : IGoogleAuthValidator
{
    private readonly string _clientId;

    public GoogleAuthValidator(IConfiguration configuration)
    {
        _clientId = configuration["GoogleSettings:ClientId"]
            ?? throw new InvalidOperationException("GoogleSettings:ClientId chưa được cấu hình.");
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _clientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserInfo(
                Email:    payload.Email,
                FullName: payload.Name ?? payload.Email);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
