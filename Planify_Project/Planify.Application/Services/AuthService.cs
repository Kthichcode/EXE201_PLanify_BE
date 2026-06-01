using Planify.Application.DTOs.Auth.Request;
using Planify.Application.DTOs.Auth.Response;
using Planify.Application.DTOs.Common;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;

namespace Planify.Application.Services;

/// <summary>
/// Business logic xác thực người dùng.
/// Không phụ thuộc vào bất kỳ thư viện hạ tầng nào (EF Core, Identity, Google SDK).
/// Tất cả tác vụ infrastructure được thực hiện qua các interface abstraction.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository       _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly ITokenService         _tokenService;
    private readonly IGoogleAuthValidator  _googleValidator;

    public AuthService(
        IUserRepository          userRepo,
        IRefreshTokenRepository  refreshTokenRepo,
        ITokenService            tokenService,
        IGoogleAuthValidator     googleValidator)
    {
        _userRepo         = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _tokenService     = tokenService;
        _googleValidator  = googleValidator;
    }

    // ── Register ─────────────────────────────────────────────────────────────

    public async Task<ResponseDto<TokenResponseDto>> RegisterAsync(RegisterRequestDto dto)
    {
        var existing = await _userRepo.FindByEmailAsync(dto.Email);
        if (existing is not null)
            return ResponseDto<TokenResponseDto>.Fail("Email đã được sử dụng.", 400);

        var (succeeded, errors) = await _userRepo.CreateWithPasswordAsync(dto.Email, dto.FullName, dto.Password);
        if (!succeeded)
            return ResponseDto<TokenResponseDto>.Fail($"Đăng ký thất bại: {string.Join(", ", errors)}", 400);

        var user = await _userRepo.FindByEmailAsync(dto.Email);
        var tokenResponse = await BuildTokenResponseAsync(user!);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Đăng ký thành công.", 201);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public async Task<ResponseDto<TokenResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _userRepo.FindByEmailAsync(dto.Email);
        if (user is null)
            return ResponseDto<TokenResponseDto>.Fail("Email hoặc mật khẩu không đúng.", 401);

        var passwordValid = await _userRepo.CheckPasswordAsync(user.Id, dto.Password);
        if (!passwordValid)
            return ResponseDto<TokenResponseDto>.Fail("Email hoặc mật khẩu không đúng.", 401);

        var tokenResponse = await BuildTokenResponseAsync(user);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Đăng nhập thành công.", 200);
    }

    // ── RefreshToken ──────────────────────────────────────────────────────────

    public async Task<ResponseDto<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        var storedToken = await _refreshTokenRepo.FindByTokenAsync(dto.RefreshToken);

        if (storedToken is null)
            return ResponseDto<TokenResponseDto>.Fail("Refresh token không hợp lệ.", 401);

        if (storedToken.IsRevoked)
            return ResponseDto<TokenResponseDto>.Fail("Refresh token đã bị thu hồi.", 401);

        if (storedToken.ExpiresAt < DateTime.UtcNow)
            return ResponseDto<TokenResponseDto>.Fail("Refresh token đã hết hạn.", 401);

        var user = await _userRepo.FindByIdAsync(storedToken.UserId);
        if (user is null)
            return ResponseDto<TokenResponseDto>.Fail("Người dùng không tồn tại.", 401);

        // Thu hồi refresh token cũ
        storedToken.IsRevoked = true;

        // Cấp token mới
        var tokenResponse = await BuildTokenResponseAsync(user);
        await _refreshTokenRepo.SaveChangesAsync();

        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Làm mới token thành công.", 200);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    public async Task<ResponseDto<object>> LogoutAsync(RefreshTokenRequestDto dto)
    {
        var storedToken = await _refreshTokenRepo.FindByTokenAsync(dto.RefreshToken);

        if (storedToken is null || storedToken.IsRevoked)
            return ResponseDto<object>.Fail("Refresh token không hợp lệ hoặc đã đăng xuất.", 400);

        storedToken.IsRevoked = true;
        await _refreshTokenRepo.SaveChangesAsync();

        return ResponseDto<object>.Success(new { }, "Đăng xuất thành công.", 200);
    }

    // ── Google Login ──────────────────────────────────────────────────────────

    public async Task<ResponseDto<TokenResponseDto>> GoogleLoginAsync(GoogleLoginRequestDto dto)
    {
        // Xác thực ID Token với Google (được thực hiện bởi Infrastructure)
        var googleUser = await _googleValidator.ValidateAsync(dto.IdToken);
        if (googleUser is null)
            return ResponseDto<TokenResponseDto>.Fail("Google ID Token không hợp lệ hoặc đã hết hạn.", 401);

        // Tìm user — tự tạo nếu chưa có
        var user = await _userRepo.FindByEmailAsync(googleUser.Email);
        if (user is null)
        {
            var (succeeded, errors) = await _userRepo.CreateExternalAsync(googleUser.Email, googleUser.FullName);
            if (!succeeded)
                return ResponseDto<TokenResponseDto>.Fail($"Tạo tài khoản thất bại: {string.Join(", ", errors)}", 400);

            user = await _userRepo.FindByEmailAsync(googleUser.Email);
        }

        var tokenResponse = await BuildTokenResponseAsync(user!);
        return ResponseDto<TokenResponseDto>.Success(tokenResponse, "Đăng nhập Google thành công.", 200);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<TokenResponseDto> BuildTokenResponseAsync(UserAccountDto user)
    {
        var roles = await _userRepo.GetRolesAsync(user.Id);
        var (accessToken, accessExpiration)   = _tokenService.GenerateAccessToken(user.Id, user.Email, user.FullName, roles);
        var (refreshToken, refreshExpiration) = _tokenService.GenerateRefreshToken();

        await _refreshTokenRepo.AddAsync(new RefreshToken
        {
            UserId    = user.Id,
            Token     = refreshToken,
            ExpiresAt = refreshExpiration,
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        });
        await _refreshTokenRepo.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken              = accessToken,
            RefreshToken             = refreshToken,
            Email                    = user.Email,
            FullName                 = user.FullName,
            AccessTokenExpiration    = accessExpiration,
            RefreshTokenExpiration   = refreshExpiration
        };
    }
}
