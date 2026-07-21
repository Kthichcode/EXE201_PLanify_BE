using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User.Response;
using Planify.Application.Interfaces;
using System;
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

    /// <summary>
    /// Thống kê tăng trưởng người dùng theo khoảng thời gian (Dành cho Admin).
    /// Nếu không truyền from/to sẽ mặc định 30 ngày gần nhất.
    /// </summary>
    /// <param name="from">Ngày bắt đầu (yyyy-MM-dd). Ví dụ: 2026-07-01</param>
    /// <param name="to">Ngày kết thúc (yyyy-MM-dd). Ví dụ: 2026-07-19</param>
    [HttpGet("growth-stats")]
    [ProducesResponseType(typeof(ResponseDto<UserGrowthStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<UserGrowthStatsDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGrowthStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var response = await _userService.GetUserGrowthStatsAsync(from, to);
        return StatusCode(response.StatusCode, response);
    }
}
