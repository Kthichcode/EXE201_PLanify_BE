using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Plans;
using Planify.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

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
}
