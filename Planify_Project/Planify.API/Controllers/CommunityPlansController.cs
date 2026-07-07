using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Community;
using Planify.Application.DTOs.Feedback;
using Planify.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/community-plans")]
public class CommunityPlansController : ControllerBase
{
    private readonly ICommunityPlanService _communityPlanService;
    private readonly IPlanFeedbackService _feedbackService;

    public CommunityPlansController(
        ICommunityPlanService communityPlanService,
        IPlanFeedbackService feedbackService)
    {
        _communityPlanService = communityPlanService;
        _feedbackService      = feedbackService;
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private Guid RequireUserId()
    {
        var id = GetCurrentUserId();
        if (id == null) throw new UnauthorizedAccessException("User ID not found or invalid.");
        return id.Value;
    }

    // ── Browse library (public) ─────────────────────────────────────────────

    /// <summary>Duyệt thư viện community plans (không cần đăng nhập).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetLibrary([FromQuery] CommunityPlanQueryDto query)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _communityPlanService.GetLibraryAsync(query, currentUserId);
        return Ok(result);
    }

    /// <summary>Xem chi tiết 1 community plan (không cần đăng nhập).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var plan = await _communityPlanService.GetCommunityPlanByIdAsync(id, currentUserId);
        if (plan == null) return NotFound(new { error = "Không tìm thấy community plan." });
        return Ok(plan);
    }

    // ── User actions (cần đăng nhập) ────────────────────────────────────────

    /// <summary>Publish plan của mình lên thư viện.</summary>
    [HttpPost("publish")]
    [Authorize]
    public async Task<IActionResult> PublishPlan([FromBody] PublishPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var userId = RequireUserId();
            var result = await _communityPlanService.PublishPlanAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
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
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>Sao chép plan từ thư viện về tài khoản cá nhân.</summary>
    [HttpPost("{id:guid}/copy")]
    [Authorize]
    public async Task<IActionResult> CopyPlan(Guid id)
    {
        try
        {
            var userId = RequireUserId();
            var newPlan = await _communityPlanService.CopyPlanToUserAsync(id, userId);
            return Ok(new { message = "Đã sao chép plan về tài khoản của bạn!", plan = newPlan });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
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
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>Toggle like/unlike cho một community plan.</summary>
    [HttpPost("{id:guid}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(Guid id)
    {
        try
        {
            var userId = RequireUserId();
            var isLiked = await _communityPlanService.ToggleLikeAsync(id, userId);
            return Ok(new
            {
                isLiked,
                message = isLiked ? "Đã thích plan này!" : "Đã bỏ thích plan này."
            });
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
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>Gỡ plan của mình khỏi thư viện.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UnpublishPlan(Guid id)
    {
        try
        {
            var userId = RequireUserId();
            await _communityPlanService.UnpublishPlanAsync(id, userId, isAdmin: false);
            return Ok(new { message = "Đã gỡ plan khỏi thư viện." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>Danh sách các community plans mà user hiện tại đã publish.</summary>
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyPublishedPlans()
    {
        try
        {
            var userId = RequireUserId();
            var result = await _communityPlanService.GetMyPublishedPlansAsync(userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    // ── Feedback (Khảo sát hiệu quả AI Plan) ───────────────────────────────

    /// <summary>
    /// Gửi khảo sát đánh giá hiệu quả kế hoạch AI sau khi publish.
    /// Chỉ áp dụng cho plan được tạo bởi AI (IsAIGenerated = true).
    /// Mỗi user chỉ được gửi 1 lần cho mỗi plan.
    /// </summary>
    [HttpPost("feedback")]
    [Authorize]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var userId = RequireUserId();
            var result = await _feedbackService.SubmitFeedbackAsync(dto, userId);
            return Ok(new { message = "Cảm ơn bạn đã gửi phản hồi!", feedback = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
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
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }
}
