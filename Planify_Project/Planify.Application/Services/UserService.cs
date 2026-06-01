using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User.Response;
using Planify.Application.Interfaces;
using Planify.Domain.Interfaces;

namespace Planify.Application.Services;

/// <summary>
/// Business logic cho thông tin người dùng.
/// Không phụ thuộc vào bất kỳ thư viện hạ tầng nào.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;

    public UserService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<ResponseDto<UserProfileResponseDto>> GetProfileAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return ResponseDto<UserProfileResponseDto>.Fail("UserId không hợp lệ.", 400);

        var user = await _userRepo.FindByIdAsync(guid);
        if (user is null)
            return ResponseDto<UserProfileResponseDto>.Fail("Không tìm thấy người dùng.", 404);

        var profile = new UserProfileResponseDto
        {
            Id       = user.Id,
            Email    = user.Email,
            FullName = user.FullName
        };

        return ResponseDto<UserProfileResponseDto>.Success(profile, "Lấy thông tin thành công.", 200);
    }
}
