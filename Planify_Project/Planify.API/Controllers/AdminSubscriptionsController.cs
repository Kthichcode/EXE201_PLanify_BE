using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Subscriptions;
using Planify.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Roles = "Admin")]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public AdminSubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Lấy toàn bộ danh sách gói subscription bao gồm cả các gói không hoạt động (Admin)
    /// </summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPlans()
    {
        var response = await _subscriptionService.GetAllPlansAsync();
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Tạo mới một gói subscription (Admin)
    /// </summary>
    [HttpPost("plans")]
    [ProducesResponseType(typeof(ResponseDto<SubscriptionPlanDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSubscriptionPlanDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _subscriptionService.CreatePlanAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Cập nhật thông tin gói subscription (Admin)
    /// </summary>
    [HttpPut("plans/{id}")]
    [ProducesResponseType(typeof(ResponseDto<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateSubscriptionPlanDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _subscriptionService.UpdatePlanAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Vô hiệu hóa một gói subscription (Admin)
    /// </summary>
    [HttpDelete("plans/{id}")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePlan(Guid id)
    {
        var response = await _subscriptionService.DeactivatePlanAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}
