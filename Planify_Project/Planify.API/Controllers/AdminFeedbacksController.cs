using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Feedback;
using Planify.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/admin/feedbacks")]
[Authorize(Roles = "Admin")]
public class AdminFeedbacksController : ControllerBase
{
    private readonly IGeneralFeedbackService _feedbackService;

    public AdminFeedbacksController(IGeneralFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>
    /// Lấy tất cả general feedback — có thể lọc theo category và status, phân trang.
    /// Category: ai_quality | ui_ux | performance | bug_report | feature_request | content | subscription | general
    /// Status: pending | reviewed | resolved
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _feedbackService.GetAllAsync(category, status, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>Xem chi tiết một feedback.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _feedbackService.GetByIdAsync(id);
            if (result == null) return NotFound(new { error = "Không tìm thấy feedback." });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật trạng thái xử lý feedback.
    /// Status hợp lệ: pending | reviewed | resolved
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateFeedbackStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _feedbackService.UpdateStatusAsync(id, dto.Status);
            return Ok(new { message = $"Đã cập nhật trạng thái thành '{dto.Status}'.", feedback = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }
}
