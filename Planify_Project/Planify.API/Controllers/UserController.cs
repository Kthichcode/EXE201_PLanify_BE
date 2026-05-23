using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User.Response;
using Planify.Application.Interfaces;
using System.Security.Claims;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Xem thông tin profile của người dùng đang đăng nhập</summary>
    /// <returns>Thông tin cá nhân: Id, Email, FullName, PhoneNumber, Roles</returns>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ResponseDto<UserProfileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<UserProfileResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseDto<UserProfileResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        // Lấy userId từ JWT claim (claim "sub")
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ResponseDto<UserProfileResponseDto>.Fail("Không xác định được người dùng.", 401));

        var response = await _userService.GetProfileAsync(userId);
        return StatusCode(response.StatusCode, response);
    }
}
