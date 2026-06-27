using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User;
using Planify.Application.DTOs.User.Response;

namespace Planify.Application.Interfaces;

public interface IUserService
{
    Task<ResponseDto<UserProfileResponseDto>> GetProfileAsync(string userId);
    Task<ResponseDto<IEnumerable<UserAdminResponseDto>>> GetAllUsersForAdminAsync();

    /// <summary>Lấy trạng thái onboarding tour của user hiện tại.</summary>
    Task<ResponseDto<OnboardingStatusDto>> GetOnboardingStatusAsync(string userId);

    /// <summary>Cập nhật trạng thái và bước onboarding (start / next step / complete / skip / stop).</summary>
    Task<ResponseDto<OnboardingStatusDto>> UpdateOnboardingAsync(string userId, UpdateOnboardingDto dto);
}
