using Microsoft.AspNetCore.Identity;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User.Response;
using Planify.Application.Interfaces;
using Planify.Infrastructure.Identity;

namespace Planify.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ResponseDto<UserProfileResponseDto>> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ResponseDto<UserProfileResponseDto>.Fail("Không tìm thấy người dùng.", 404);

        var profile = new UserProfileResponseDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName
        };

        return ResponseDto<UserProfileResponseDto>.Success(profile, "Lấy thông tin thành công.", 200);
    }
}
