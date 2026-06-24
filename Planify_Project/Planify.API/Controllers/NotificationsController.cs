using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Notifications;
using Planify.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(userIdString, out var userId))
            return userId;
        throw new UnauthorizedAccessException("Invalid user token.");
    }

    /// <summary>
    /// Lấy danh sách thông báo của người dùng (tối đa 50 cái mới nhất)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications([FromQuery] int limit = 50)
    {
        var response = await _notificationService.GetUserNotificationsAsync(GetUserId(), limit);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Lấy số lượng thông báo chưa đọc
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ResponseDto<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var response = await _notificationService.GetUnreadCountAsync(GetUserId());
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Đánh dấu 1 thông báo là đã đọc
    /// </summary>
    [HttpPut("{id}/read")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var response = await _notificationService.MarkAsReadAsync(GetUserId(), id);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Đánh dấu TẤT CẢ thông báo là đã đọc
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var response = await _notificationService.MarkAllAsReadAsync(GetUserId());
        return StatusCode(response.StatusCode, response);
    }
}
