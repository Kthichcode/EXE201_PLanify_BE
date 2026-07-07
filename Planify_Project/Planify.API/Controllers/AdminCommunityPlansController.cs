using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Community;
using Planify.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/admin/community-plans")]
[Authorize(Roles = "Admin")]
public class AdminCommunityPlansController : ControllerBase
{
    private readonly ICommunityPlanService _communityPlanService;
    private readonly IPlanFeedbackService _feedbackService;

    public AdminCommunityPlansController(
        ICommunityPlanService communityPlanService,
        IPlanFeedbackService feedbackService)
    {
        _communityPlanService = communityPlanService;
        _feedbackService      = feedbackService;
    }

    private Guid GetAdminId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var id))
            throw new UnauthorizedAccessException("Admin ID không hợp lệ.");
        return id;
    }

    /// <summary>Danh sách community plans đang chờ duyệt.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _communityPlanService.GetPendingPlansAsync(page, pageSize);
        return Ok(result);
    }

    /// <summary>Xem chi tiết bất kỳ community plan (kèm đầy đủ tasks + subtasks) để admin review trước khi duyệt.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var result = await _communityPlanService.GetCommunityPlanDetailForAdminAsync(id);
        if (result == null) return NotFound(new { error = "Không tìm thấy community plan." });
        return Ok(result);
    }

    /// <summary>Duyệt một community plan (chuyển sang approved).</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        try
        {
            var adminId = GetAdminId();
            var result = await _communityPlanService.ApprovePlanAsync(id, adminId);
            return Ok(new { message = "Đã duyệt plan thành công.", communityPlan = result });
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

    /// <summary>Từ chối một community plan kèm lý do.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var adminId = GetAdminId();
            var result = await _communityPlanService.RejectPlanAsync(id, adminId, dto.Reason);
            return Ok(new { message = "Đã từ chối plan.", communityPlan = result });
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

    /// <summary>Admin gỡ bất kỳ plan nào khỏi thư viện.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemovePlan(Guid id)
    {
        try
        {
            var adminId = GetAdminId();
            await _communityPlanService.UnpublishPlanAsync(id, adminId, isAdmin: true);
            return Ok(new { message = "Đã gỡ plan khỏi thư viện." });
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

    // ── Feedback (Khảo sát hiệu quả AI Plan) ───────────────────────────────

    /// <summary>
    /// Xem toàn bộ feedback khảo sát AI plan (có phân trang).
    /// </summary>
    [HttpGet("feedbacks")]
    public async Task<IActionResult> GetAllFeedbacks([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _feedbackService.GetAllFeedbackAsync(page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }

    /// <summary>
    /// Xem tất cả feedback khảo sát của một plan cụ thể.
    /// </summary>
    [HttpGet("{id:guid}/feedbacks")]
    public async Task<IActionResult> GetFeedbacksByPlan(Guid id)
    {
        try
        {
            var result = await _feedbackService.GetFeedbackByPlanAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Đã có lỗi xảy ra.", details = ex.Message });
        }
    }
}
