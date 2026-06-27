using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Plans;
using Planify.Application.Interfaces;
using Planify.Application.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace Planify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlansController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("User ID not found or invalid.");
        }

        try
        {
            var plan = await _planService.CreateManualPlanAsync(dto, userId);
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while creating the plan. " + ex.Message);
        }
    }

    [HttpPost("{planId}/tasks")]
    public async Task<IActionResult> AddTaskToPlan(Guid planId, [FromBody] CreatePlanTaskDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("User ID not found or invalid.");
        }

        try
        {
            var task = await _planService.AddTaskToPlanAsync(planId, dto, userId);
            return Ok(task);
        }
        catch (Exception ex)
        {
            // If it's a "not found or no permission" error, it's generally better to return 404 or 403
            if (ex.Message.Contains("not found"))
            {
                return NotFound(ex.Message);
            }
            return StatusCode(500, "An error occurred while adding the task. " + ex.Message);
        }
    }

    [HttpPut("{planId}/tasks/{taskId}/status")]
    public async Task<IActionResult> UpdateTaskStatus(Guid planId, Guid taskId, [FromBody] UpdateTaskStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("User ID not found or invalid.");
        }

        try
        {
            var task = await _planService.UpdateTaskStatusAsync(planId, taskId, dto, userId);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not found") || ex.Message.Contains("access denied"))
            {
                return NotFound(ex.Message);
            }
            return StatusCode(500, "An error occurred while updating the task status. " + ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlan(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("User ID not found or invalid.");
        }

        var plan = await _planService.GetPlanByIdAsync(id, userId);
        if (plan == null)
        {
            return NotFound("Plan not found or you do not have permission to access it.");
        }

        return Ok(plan);
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmPlan(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized("User ID not found or invalid.");

        try
        {
            var plan = await _planService.ConfirmDraftPlanAsync(id, userId);
            return Ok(new { message = "Kế hoạch đã được xác nhận!", plan });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Có lỗi xảy ra khi xác nhận kế hoạch.", details = ex.Message });
        }
    }

    [HttpDelete("{id}/draft")]
    public async Task<IActionResult> DiscardDraft(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized("User ID not found or invalid.");

        try
        {
            await _planService.DiscardDraftPlanAsync(id, userId);
            return Ok(new { message = "Đã hủy bản nháp kế hoạch." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Có lỗi xảy ra khi hủy bản nháp.", details = ex.Message });
        }
    }


    
[HttpGet]
public async Task<IActionResult> GetPlans()
{
    var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
    {
        return Unauthorized("User ID not found or invalid.");
    }

    try
    {
        var plans = await _planService.GetPlansByUserIdAsync(userId);
        return Ok(plans);
    }
    catch (Exception ex)
    {
        return StatusCode(500, "An error occurred while retrieving plans. " + ex.Message);
    }
}


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized("User ID not found or invalid.");

        try
        {
            await _planService.DeletePlanAsync(id, userId);
            return Ok(new { message = "Kế hoạch đã được xóa thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Có lỗi xảy ra khi xóa kế hoạch.", details = ex.Message });
        }
    }

    [HttpPut("{planId}/tasks/{taskId}")]
    public async Task<IActionResult> UpdatePlanTask(Guid planId, Guid taskId, [FromBody] UpdatePlanTaskDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { error = "User ID not found or invalid." });

        try
        {
            var task = await _planService.UpdatePlanTaskAsync(planId, taskId, dto, userId);
            return Ok(task);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Có lỗi xảy ra khi cập nhật nhiệm vụ.", details = ex.Message });
        }
    }

    [HttpDelete("{planId}/tasks/{taskId}")]
    public async Task<IActionResult> DeletePlanTask(Guid planId, Guid taskId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { error = "User ID not found or invalid." });

        try
        {
            await _planService.DeletePlanTaskAsync(planId, taskId, userId);
            return Ok(new { message = "Nhiệm vụ đã được xóa thành công." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Có lỗi xảy ra khi xóa nhiệm vụ.", details = ex.Message });
        }
    }
}
