using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.AI;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly IPlanService _planService;
    private readonly IPlanFrameworkRepository _frameworkRepo;
    private readonly IPlanTemplateRepository _templateRepo;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(
        IAiChatService aiChatService,
        IPlanService planService,
        IPlanFrameworkRepository frameworkRepo,
        IPlanTemplateRepository templateRepo,
        ILogger<AiChatController> logger)
    {
        _aiChatService  = aiChatService;
        _planService    = planService;
        _frameworkRepo  = frameworkRepo;
        _templateRepo   = templateRepo;
        _logger         = logger;
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
    /// Hệ thống sẽ auto-detect loại kế hoạch từ prompt và inject template tương ứng vào AI.
    /// FE có thể truyền TemplateId để chỉ định template cụ thể (bỏ qua auto-detect).
    /// </summary>
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
            // 1. Resolve template từ DB
            var (templateContext, resolvedTemplate, resolvedFramework) =
                await ResolveTemplateAsync(request, cancellationToken);

            if (resolvedTemplate != null)
                _logger.LogInformation(
                    "Template resolved: templateId={TemplateId}, frameworkId={FrameworkId}",
                    resolvedTemplate.Id, resolvedFramework?.Id);

            // 2. Gọi AI → nhận JSON kế hoạch (với template context nếu có)
            var aiResponse = await _aiChatService.GeneratePlanAsync(
                request, templateContext, cancellationToken);

            // 3. Parse JSON → lưu DB với Status = "draft" (hết hạn sau 24h)
            var saveDto = new SaveAiPlanRequestDto
            {
                PlanData    = aiResponse.PlanData,
                TemplateId  = resolvedTemplate?.Id,
                FrameworkId = resolvedFramework?.Id
            };
            var draftPlan = await _planService.SaveAiPlanAsDraftAsync(saveDto, userId.Value);

            _logger.LogInformation(
                "AI draft plan created: planId={PlanId}, userId={UserId}, elapsedMs={Ms}",
                draftPlan.Id, userId.Value, aiResponse.ElapsedMs);

            return Ok(new
            {
                planId        = draftPlan.Id,
                plan          = draftPlan,
                message       = aiResponse.Message,
                model         = aiResponse.Model,
                elapsedMs     = aiResponse.ElapsedMs,
                usedTemplateId   = resolvedTemplate?.Id,
                usedTemplateName = resolvedTemplate?.Title,
                usedFrameworkId  = resolvedFramework?.Id,
                usedFrameworkName= resolvedFramework?.Name
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

    // ── POST /api/ai/analyze-delay ────────────────────────────────────────

    /// <summary>
    /// Phân tích tình trạng trễ tiến độ của plan và đề xuất tối ưu lại lịch trình (chưa lưu DB).
    /// FE gọi tự động khi user bấm vào thông báo delay_alert.
    /// AI trả về JSON plan đề xuất + strategy ("reschedule" | "extend_deadline") + giải thích.
    /// </summary>
    [HttpPost("analyze-delay")]
    public async Task<IActionResult> AnalyzeDelay(
        [FromBody] AnalyzeDelayRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { error = "Không xác định được user từ token." });

        try
        {
            // 1. Load plan + tasks hiện tại
            var currentPlan = await _planService.GetPlanByIdAsync(request.PlanId, userId.Value);
            if (currentPlan is null)
                return NotFound(new { error = $"Không tìm thấy kế hoạch với id={request.PlanId}." });

            // 2. Tính số subtask trễ và số ngày còn lại đến deadline
            // So sánh UTC với UTC trực tiếp — không ToLocalTime() để tránh double-convert timezone.
            var nowUtc = DateTime.UtcNow;
            var overdueCount = currentPlan.Tasks?
                .Where(t => t.ParentTaskId != null
                         && t.Status != "done"
                         && t.DueDate.HasValue
                         && t.DueDate.Value.ToUniversalTime() < nowUtc)
                .Count() ?? 0;

            int daysToDeadline = currentPlan.Deadline.HasValue
                ? (int)(currentPlan.Deadline.Value.ToUniversalTime() - nowUtc).TotalDays
                : 999;

            // 3. Serialize plan hiện tại để gửi AI
            var currentPlanJson = System.Text.Json.JsonSerializer.Serialize(currentPlan,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = null });

            // 4. AI phân tích — CHỈ trả về đề xuất, KHÔNG lưu DB
            var aiResponse = await _aiChatService.AnalyzeDelayAsync(
                currentPlanJson, overdueCount, daysToDeadline,
                request.ForceStrategy,
                cancellationToken);

            // 5. Đọc strategy từ metadata AI trả về
            var strategy = aiResponse.PlanData?["metadata"]?["strategy"]?.GetValue<string>()
                        ?? (daysToDeadline > 7 ? "reschedule" : "extend_deadline");

            _logger.LogInformation(
                "Delay analysis done: planId={PlanId}, overdueCount={Count}, daysToDeadline={Days}, strategy={Strategy}, elapsedMs={Ms}",
                request.PlanId, overdueCount, daysToDeadline, strategy, aiResponse.ElapsedMs);

            return Ok(new AnalyzeDelayResponseDto
            {
                ProposedPlanData = aiResponse.PlanData,
                Message          = aiResponse.Message,
                Strategy         = strategy,
                OverdueCount     = overdueCount,
                DaysToDeadline   = daysToDeadline,
                Model            = aiResponse.Model,
                ElapsedMs        = aiResponse.ElapsedMs
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
            _logger.LogWarning(ex, "Analyze delay thất bại");
            return StatusCode(503, new { error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, new { error = "AI xử lý quá lâu. Vui lòng thử lại." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analyze delay thất bại không mong đợi");
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // ── POST /api/ai/apply-delay-fix ──────────────────────────────────────

    /// <summary>
    /// User bấm "Đồng ý" sau khi xem đề xuất AI → apply JSON plan đề xuất vào DB.
    /// Dùng lại RefreshDraftWithRefinedPlanAsync để bảo toàn tiến độ đã làm.
    /// </summary>
    [HttpPost("apply-delay-fix")]
    public async Task<IActionResult> ApplyDelayFix(
        [FromBody] ApplyDelayFixRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { error = "Không xác định được user từ token." });

        try
        {
            // Dùng lại RefreshDraftWithRefinedPlanAsync — đã có logic bảo toàn tiến độ (snapshot/restore)
            var saveDto = new SaveAiPlanRequestDto { PlanData = request.PlanData };
            var updatedPlan = await _planService.RefreshDraftWithRefinedPlanAsync(
                request.PlanId, saveDto, userId.Value);

            _logger.LogInformation(
                "Delay fix applied: planId={PlanId}, userId={UserId}",
                updatedPlan.Id, userId.Value);

            return Ok(new
            {
                message = "Kế hoạch đã được tối ưu lại thành công!",
                planId  = updatedPlan.Id,
                plan    = updatedPlan
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
            _logger.LogWarning(ex, "Apply delay fix thất bại");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply delay fix thất bại không mong đợi");
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Resolve template từ DB:
    /// 1. Nếu request có TemplateId → dùng thẳng.
    /// 2. Nếu không → auto-detect framework qua keyword rồi lấy template active đầu tiên.
    /// Trả về (templateContext, resolvedTemplate, resolvedFramework).
    /// </summary>
    private async Task<(string? templateContext, PlanTemplate? template, PlanFramework? framework)>
        ResolveTemplateAsync(GeneratePlanRequestDto request, CancellationToken ct)
    {
        PlanTemplate? template = null;
        PlanFramework? framework = null;

        if (request.TemplateId.HasValue)
        {
            // FE chỉ định template cụ thể
            template = await _templateRepo.GetByIdAsync(request.TemplateId.Value, ct);
            if (template != null)
                framework = template.Framework;
        }
        else
        {
            // Auto-detect framework từ keyword trong prompt
            framework = await _frameworkRepo.FindByKeywordAsync(request.Prompt, ct);
            if (framework != null)
            {
                // Lấy template active đầu tiên của framework này
                var templates = await _templateRepo.GetByFrameworkIdAsync(framework.Id, ct);
                template = templates.FirstOrDefault(t => t.IsActive);
            }
        }

        if (template == null)
            return (null, null, framework);

        // Format template content thành chuỗi compact để inject vào AI (tiết kiệm token)
        var templateContext = FormatTemplateForAi(template);
        return (templateContext, template, framework);
    }

    /// <summary>
    /// Format template content thành chuỗi ngắn gọn cho AI.
    /// Chỉ trích xuất tên framework, tiêu đề template, và danh sách task titles từ JSON.
    /// </summary>
    private static string FormatTemplateForAi(PlanTemplate template)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Template: {template.Title}");
        if (!string.IsNullOrWhiteSpace(template.Description))
            sb.AppendLine($"Mô tả: {template.Description}");

        try
        {
            var node = JsonNode.Parse(template.TemplateContent);
            if (node is JsonObject obj)
            {
                // Trích xuất tasks nếu có
                var tasks = obj["tasks"]?.AsArray();
                if (tasks != null && tasks.Count > 0)
                {
                    sb.AppendLine("Cấu trúc tasks mẫu:");
                    foreach (var task in tasks)
                    {
                        var taskTitle = task?["Title"]?.GetValue<string>() ?? task?["title"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(taskTitle)) continue;

                        sb.AppendLine($"- {taskTitle}");

                        var subtasks = task?["subtasks"]?.AsArray();
                        if (subtasks != null)
                        {
                            foreach (var sub in subtasks)
                            {
                                var subTitle = sub?["Title"]?.GetValue<string>() ?? sub?["title"]?.GetValue<string>();
                                if (!string.IsNullOrWhiteSpace(subTitle))
                                    sb.AppendLine($"  + {subTitle}");
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Nếu TemplateContent không phải JSON hợp lệ → dùng raw text
            sb.AppendLine(template.TemplateContent);
        }

        return sb.ToString().Trim();
    }
}

