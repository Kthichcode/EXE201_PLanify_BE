using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User.Response;
using Planify.Application.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lấy toàn bộ danh sách người dùng trong hệ thống kèm thông tin gói đăng ký (Dành cho Admin)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<UserAdminResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var response = await _userService.GetAllUsersForAdminAsync();
        return StatusCode(response.StatusCode, response);
    }
}
