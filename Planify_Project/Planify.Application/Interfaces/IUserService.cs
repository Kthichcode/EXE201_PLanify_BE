using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User.Response;

namespace Planify.Application.Interfaces;

public interface IUserService
{
    Task<ResponseDto<UserProfileResponseDto>> GetProfileAsync(string userId);
}
