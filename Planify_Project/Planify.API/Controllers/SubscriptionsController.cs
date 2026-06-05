using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS;
using PayOS.Models;
using PayOS.Models.Webhooks;
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
    private readonly PayOSClient _payOSClient;

    public SubscriptionsController(ISubscriptionService subscriptionService, PayOSClient payOSClient)
    {
        _subscriptionService = subscriptionService;
        _payOSClient = payOSClient;
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
    [ProducesResponseType(typeof(ResponseDto<UpgradeSubscriptionResultDto>), StatusCodes.Status200OK)]
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
            return Unauthorized(ResponseDto<UpgradeSubscriptionResultDto>.Fail("Không xác định được người dùng.", 401));
        }

        var response = await _subscriptionService.UpgradeSubscriptionAsync(userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Webhook nhận thông báo thanh toán từ PayOS (Server-to-Server)
    /// </summary>
    [HttpPost("payos-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePayOSWebhook([FromBody] Webhook body)
    {
        try
        {
            if (body == null)
            {
                return BadRequest(ResponseDto<bool>.Fail("Dữ liệu webhook không hợp lệ.", 400));
            }

            var verifiedData = await _payOSClient.Webhooks.VerifyAsync(body);
            
            // Cập nhật trạng thái thanh toán thành công trong database
            var confirmResult = await _subscriptionService.ConfirmPaymentAsync(verifiedData.OrderCode, "PAID", verifiedData.Reference);
            if (confirmResult.StatusCode < 200 || confirmResult.StatusCode >= 300)
            {
                return BadRequest(confirmResult);
            }

            return Ok(ResponseDto<bool>.Success(true, "Xử lý webhook PayOS thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ResponseDto<bool>.Fail($"Lỗi xác thực webhook hoặc xử lý: {ex.Message}", 400));
        }
    }

    /// <summary>
    /// Đồng bộ trạng thái thanh toán của một hóa đơn từ PayOS
    /// </summary>
    [HttpGet("check-status/{orderCode}")]
    public async Task<IActionResult> CheckPaymentStatus([FromRoute] long orderCode)
    {
        try
        {
            var paymentInfo = await _payOSClient.PaymentRequests.GetAsync(orderCode);
            var confirmResult = await _subscriptionService.ConfirmPaymentAsync(orderCode, paymentInfo.Status.ToString(), paymentInfo.Id);
            if (confirmResult.StatusCode < 200 || confirmResult.StatusCode >= 300)
            {
                return StatusCode(confirmResult.StatusCode, confirmResult);
            }
            return Ok(ResponseDto<string>.Success(paymentInfo.Status.ToString(), $"Trạng thái thanh toán hiện tại: {paymentInfo.Status}"));
        }
        catch (Exception ex)
        {
            return BadRequest(ResponseDto<string>.Fail($"Lỗi khi kiểm tra trạng thái thanh toán: {ex.Message}", 400));
        }
    }
}
