using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Feedback;
using Planify.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/feedbacks")]
[Authorize]
public class FeedbacksController : ControllerBase
{
    private readonly IGeneralFeedbackService _feedbackService;

    public FeedbacksController(IGeneralFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    private Guid RequireUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var id))
            throw new UnauthorizedAccessException("User ID không hợp lệ.");
        return id;
    }

    /// <summary>
    /// Gửi phản hồi tổng quát — có thể gửi bất kỳ lúc nào, không giới hạn số lần.
    /// Hạng mục (category): ai_quality | ui_ux | performance | bug_report |
    /// feature_request | content | subscription | general
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitGeneralFeedbackDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var userId = RequireUserId();
            var result = await _feedbackService.SubmitAsync(dto, userId);
            return Ok(new { message = "Cảm ơn bạn đã gửi phản hồi!", feedback = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
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
