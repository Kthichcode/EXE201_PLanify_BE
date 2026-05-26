using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Subscriptions;
using Planify.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Lấy danh sách các gói subscription đang hoạt động (Public/User)
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePlans()
    {
        var response = await _subscriptionService.GetActivePlansAsync();
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Lấy thông tin gói subscription hiện tại của người dùng đang đăng nhập
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ResponseDto<UserSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(ResponseDto<UserSubscriptionDto>.Fail("Không xác định được người dùng.", 401));
        }

        var response = await _subscriptionService.GetUserSubscriptionAsync(userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Nâng cấp hoặc mua một gói subscription mới
    /// </summary>
    [HttpPost("upgrade")]
    [ProducesResponseType(typeof(ResponseDto<UserSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpgradeSubscription([FromBody] UpgradeSubscriptionRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(ResponseDto<UserSubscriptionDto>.Fail("Không xác định được người dùng.", 401));
        }

        var response = await _subscriptionService.UpgradeSubscriptionAsync(userId, dto);
        return StatusCode(response.StatusCode, response);
    }
}
