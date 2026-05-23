using Planify.Application.DTOs.Auth.Request;
using Planify.Application.DTOs.Auth.Response;
using Planify.Application.DTOs.Common;

namespace Planify.Application.Interfaces;

public interface IAuthService
{
    Task<ResponseDto<TokenResponseDto>> RegisterAsync(RegisterRequestDto dto);
    Task<ResponseDto<TokenResponseDto>> LoginAsync(LoginRequestDto dto);
    Task<ResponseDto<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto);
    Task<ResponseDto<object>> LogoutAsync(RefreshTokenRequestDto dto);
}
