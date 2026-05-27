using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.AI;
using Planify.Application.Interfaces;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(IAiChatService aiChatService, ILogger<AiChatController> logger)
    {
        _aiChatService = aiChatService;
        _logger = logger;
    }

    /// <summary>
    /// Chat hội thoại với Planify AI (chỉ về chủ đề kế hoạch, tiếng Việt).
    /// </summary>
    /// <remarks>
    /// Request body:
    /// <code>
    /// {
    ///   "message": "Mình nên làm gì trước khi bắt đầu kế hoạch học tập?",
    ///   "history": [
    ///     { "role": "user", "content": "Xin chào" },
    ///     { "role": "assistant", "content": "Xin chào! Mình có thể giúp gì?" }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
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

    /// <summary>
    /// Tạo kế hoạch chi tiết bằng AI từ mục tiêu và deadline.
    /// AI trả về JSON chuẩn gồm plan, tasks, subtasks và metadata.
    /// </summary>
    /// <remarks>
    /// Request body:
    /// <code>
    /// {
    ///   "goal": "Học IELTS đạt 6.5",
    ///   "deadline": "2025-08-01",
    ///   "description": "Mình cần ôn 4 kỹ năng từ đầu"
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("generate-plan")]
    public async Task<IActionResult> GeneratePlan(
        [FromBody] GeneratePlanRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate deadline format - so sánh theo ngày (GMT+7), cho phép hôm nay trở đi
        var today = DateTime.UtcNow.AddHours(7).Date;
        if (!DateTime.TryParse(request.Deadline, out var deadline) || deadline.Date < today)
            return BadRequest(new { error = "Deadline không hợp lệ. Vui lòng nhập ngày hôm nay hoặc trong tương lai (định dạng YYYY-MM-DD)." });

        try
        {
            var response = await _aiChatService.GeneratePlanAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Generate plan bị timeout hoặc bị hủy");
            return StatusCode(408, new { error = "AI xử lý quá lâu. Vui lòng thử lại (llama3 cần 30-120 giây tùy máy)." });
        }
        catch (Exception ex)
        {
            // Log đầy đủ để debug
            _logger.LogError(ex, "Generate plan thất bại. Type={Type} Message={Message}",
                ex.GetType().Name, ex.Message);

            // Trả về lỗi thật để dễ debug (có thể ẩn đi khi production)
            return ex is InvalidOperationException
                ? StatusCode(503, new { error = ex.Message, type = ex.GetType().Name })
                : StatusCode(500, new
                {
                    error   = ex.Message,
                    type    = ex.GetType().Name,
                    details = ex.InnerException?.Message
                });
        }
    }
}

