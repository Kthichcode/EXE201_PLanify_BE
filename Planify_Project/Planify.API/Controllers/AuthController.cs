using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Auth.Request;
using Planify.Application.DTOs.Auth.Response;
using Planify.Application.DTOs.Common;
using Planify.Application.Interfaces;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Đăng ký tài khoản mới</summary>
    /// <param name="dto">Email, FullName, Password</param>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        var response = await _authService.RegisterAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Đăng nhập và nhận Access Token + Refresh Token</summary>
    /// <param name="dto">Email, Password</param>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var response = await _authService.LoginAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Làm mới Access Token bằng Refresh Token</summary>
    /// <param name="dto">RefreshToken hợp lệ</param>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        var response = await _authService.RefreshTokenAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Đăng xuất — thu hồi Refresh Token</summary>
    /// <param name="dto">RefreshToken cần thu hồi</param>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        var response = await _authService.LogoutAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Đăng nhập bằng tài khoản Google</summary>
    /// <param name="dto">Google ID Token lấy từ Google Sign-In SDK phía client</param>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<TokenResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto dto)
    {
        var response = await _authService.GoogleLoginAsync(dto);
        return StatusCode(response.StatusCode, response);
    }
}
