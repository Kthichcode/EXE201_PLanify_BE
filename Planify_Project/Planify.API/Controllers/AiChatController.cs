using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.AI;
using Planify.Application.Interfaces;
using System.Security.Claims;
using System.Text.Json;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly IPlanService _planService;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(
        IAiChatService aiChatService,
        IPlanService planService,
        ILogger<AiChatController> logger)
    {
        _aiChatService = aiChatService;
        _planService   = planService;
        _logger        = logger;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    // ── POST /api/ai/chat ─────────────────────────────────────────────────

    /// <summary>Chat hội thoại với Planify AI (chỉ về chủ đề kế hoạch, tiếng Việt).</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message không được để trống." });

        try
        {
            var response = await _aiChatService.ChatAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AI chat thất bại");
            return StatusCode(503, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong đợi trong AI chat");
            return StatusCode(500, new { error = "Có lỗi xảy ra khi giao tiếp với AI." });
        }
    }

    // ── POST /api/ai/generate-plan ────────────────────────────────────────

    /// <summary>
    /// Tạo kế hoạch bằng AI → tự động lưu vào DB với Status = "draft".
    /// FE dùng planId trả về để hiển thị preview và sau đó confirm/discard.
    /// </summary>
    /// <remarks>
    /// Request body:
    /// <code>
    /// { "prompt": "Tôi muốn học IELTS 6.5 trước tháng 8/2026, hiện tại band 5.0" }
    /// </code>
    /// Response trả về: { planId, plan (PlanDto đầy đủ), message, model, elapsedMs }
    /// </remarks>
    [HttpPost("generate-plan")]
    public async Task<IActionResult> GeneratePlan(
        [FromBody] GeneratePlanRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { error = "Không xác định được user từ token." });

        try
        {
            // 1. Gọi AI → nhận JSON kế hoạch
            var aiResponse = await _aiChatService.GeneratePlanAsync(request, cancellationToken);

            // 2. Parse JSON → lưu DB với Status = "draft" (hết hạn sau 24h)
            var saveDto = new SaveAiPlanRequestDto { PlanData = aiResponse.PlanData };
            var draftPlan = await _planService.SaveAiPlanAsDraftAsync(saveDto, userId.Value);

            _logger.LogInformation(
                "AI draft plan created: planId={PlanId}, userId={UserId}, elapsedMs={Ms}",
                draftPlan.Id, userId.Value, aiResponse.ElapsedMs);

            return Ok(new
            {
                planId    = draftPlan.Id,
                plan      = draftPlan,
                message   = aiResponse.Message,
                model     = aiResponse.Model,
                elapsedMs = aiResponse.ElapsedMs
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Generate plan bị timeout hoặc bị hủy");
            return StatusCode(408, new { error = "AI xử lý quá lâu. Vui lòng thử lại." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generate plan thất bại. Type={Type}", ex.GetType().Name);
            return ex is InvalidOperationException
                ? StatusCode(503, new { error = ex.Message })
                : StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // ── POST /api/ai/refine-plan ──────────────────────────────────────────

    /// <summary>
    /// Yêu cầu AI chỉnh sửa kế hoạch draft đã tạo theo instruction của người dùng.
    /// Draft cũ bị overwrite tại chỗ (giữ nguyên planId), DraftExpiresAt được gia hạn thêm 24h.
    /// </summary>
    /// <remarks>
    /// Request body:
    /// <code>
    /// {
    ///   "planId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///   "instruction": "Thêm 2 task về marketing, rút ngắn deadline xuống cuối tháng 7"
    /// }
    /// </code>
    /// Response trả về: { planId, plan (PlanDto đầy đủ), message, model, elapsedMs }
    /// </remarks>
    [HttpPost("refine-plan")]
    public async Task<IActionResult> RefinePlan(
        [FromBody] RefinePlanRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { error = "Không xác định được user từ token." });

        try
        {
            // 1. Load plan hiện tại từ DB
            var currentPlan = await _planService.GetPlanByIdAsync(request.PlanId, userId.Value);
            if (currentPlan is null)
                return NotFound(new { error = $"Không tìm thấy kế hoạch với id={request.PlanId}." });

            // Chỉ không cho refine plan đã bị discard
            if (currentPlan.Status == "discarded")
                return BadRequest(new { error = "Không thể chỉnh sửa kế hoạch đã bị hủy." });

            // 2. Serialize plan hiện tại thành JSON để gửi cho AI
            var currentPlanJson = JsonSerializer.Serialize(currentPlan, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null  // giữ PascalCase để nhất quán với schema AI
            });

            // 3. Gọi AI → nhận JSON plan đã chỉnh sửa
            var aiResponse = await _aiChatService.RefinePlanAsync(
                currentPlanJson,
                request.Instruction,
                cancellationToken);

            // 4. Overwrite draft trong DB (cùng planId)
            var saveDto = new SaveAiPlanRequestDto { PlanData = aiResponse.PlanData };
            var refinedPlan = await _planService.RefreshDraftWithRefinedPlanAsync(
                request.PlanId,
                saveDto,
                userId.Value);

            _logger.LogInformation(
                "AI refined draft: planId={PlanId}, userId={UserId}, instruction={Instruction}, elapsedMs={Ms}",
                refinedPlan.Id, userId.Value, request.Instruction, aiResponse.ElapsedMs);

            return Ok(new
            {
                planId    = refinedPlan.Id,
                plan      = refinedPlan,
                message   = aiResponse.Message,
                model     = aiResponse.Model,
                elapsedMs = aiResponse.ElapsedMs
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Refine plan thất bại");
            return ex.Message.Contains("draft")
                ? BadRequest(new { error = ex.Message })
                : StatusCode(503, new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Refine plan bị timeout hoặc bị hủy");
            return StatusCode(408, new { error = "AI xử lý quá lâu. Vui lòng thử lại." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refine plan thất bại không mong đợi. Type={Type}", ex.GetType().Name);
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }
}
