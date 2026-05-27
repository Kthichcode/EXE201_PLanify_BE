using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planify.Application.DTOs.Auth.Request;
using Planify.Application.DTOs.Auth.Response;
using Planify.Application.DTOs.Common;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Infrastructure.Data;
using Planify.Infrastructure.Identity;

namespace Planify.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TokenService tokenService,
        ApplicationDbContext dbContext,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<ResponseDto<TokenResponseDto>> RegisterAsync(RegisterRequestDto dto)
    {
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
            return ResponseDto<TokenResponseDto>.Fail("Email đã được sử dụng.", 400);

        var user = new ApplicationUser
        {
            Email = dto.Email,
            UserName = dto.Email,
            FullName = dto.FullName
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return ResponseDto<TokenResponseDto>.Fail($"Đăng ký thất bại: {errors}", 400);
        }

        var tokenResponse = await BuildTokenResponseAsync(user);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Đăng ký thành công.", 201);
    }

    public async Task<ResponseDto<TokenResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return ResponseDto<TokenResponseDto>.Fail("Email hoặc mật khẩu không đúng.", 401);

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
            return ResponseDto<TokenResponseDto>.Fail("Email hoặc mật khẩu không đúng.", 401);

        var tokenResponse = await BuildTokenResponseAsync(user);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Đăng nhập thành công.", 200);
    }

    public async Task<ResponseDto<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        // Tìm refresh token trong DB
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken);

        if (storedToken == null)
            return ResponseDto<TokenResponseDto>.Fail("Refresh token không hợp lệ.", 401);

        if (storedToken.IsRevoked)
            return ResponseDto<TokenResponseDto>.Fail("Refresh token đã bị thu hồi.", 401);

        if (storedToken.ExpiresAt < DateTime.UtcNow)
            return ResponseDto<TokenResponseDto>.Fail("Refresh token đã hết hạn.", 401);

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user == null)
            return ResponseDto<TokenResponseDto>.Fail("Người dùng không tồn tại.", 401);

        // Thu hồi refresh token cũ
        storedToken.IsRevoked = true;

        // Cấp token mới
        var tokenResponse = await BuildTokenResponseAsync(user);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Làm mới token thành công.", 200);
    }

    public async Task<ResponseDto<object>> LogoutAsync(RefreshTokenRequestDto dto)
    {
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken);

        if (storedToken == null || storedToken.IsRevoked)
            return ResponseDto<object>.Fail("Refresh token không hợp lệ hoặc đã đăng xuất.", 400);

        // Thu hồi token
        storedToken.IsRevoked = true;
        await _dbContext.SaveChangesAsync();

        return ResponseDto<object>.Success(new { }, "Đăng xuất thành công.", 200);
    }

    public async Task<ResponseDto<TokenResponseDto>> GoogleLoginAsync(GoogleLoginRequestDto dto)
    {
        // 1. Xác minh ID Token với Google
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var clientId = _configuration["GoogleSettings:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
        }
        catch (InvalidJwtException)
        {
            return ResponseDto<TokenResponseDto>.Fail("Google ID Token không hợp lệ hoặc đã hết hạn.", 401);
        }

        // 2. Tìm user theo email — nếu chưa có thì tự tạo
        var user = await _userManager.FindByEmailAsync(payload.Email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                Email = payload.Email,
                UserName = payload.Email,
                FullName = payload.Name ?? payload.Email,
                EmailConfirmed = true   // Google đã xác minh email
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return ResponseDto<TokenResponseDto>.Fail($"Tạo tài khoản thất bại: {errors}", 400);
            }
        }

        // 3. Cấp token của app
        var tokenResponse = await BuildTokenResponseAsync(user);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Đăng nhập Google thành công.", 200);
    }

    // ----- Helper -----

    private async Task<TokenResponseDto> BuildTokenResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, accessExpiration) = _tokenService.GenerateAccessToken(user, roles);
        var (refreshToken, refreshExpiration) = _tokenService.GenerateRefreshToken();

        // Lưu refresh token vào DB
        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = refreshExpiration,
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        });
        await _dbContext.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Email = user.Email!,
            FullName = user.FullName,
            AccessTokenExpiration = accessExpiration,
            RefreshTokenExpiration = refreshExpiration
        };
    }
}

//Datchyms
//Datchyms