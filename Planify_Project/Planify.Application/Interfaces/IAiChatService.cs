using Planify.Application.DTOs.AI;

namespace Planify.Application.Interfaces;

public interface IAiChatService
{
    /// <summary>
    /// Chat thông thường với AI (chỉ về chủ đề kế hoạch, tiếng Việt).
    /// </summary>
    Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo kế hoạch chi tiết từ goal/deadline/description.
    /// AI trả về JSON chuẩn theo cấu trúc Planify.
    /// </summary>
    Task<GeneratePlanResponseDto> GeneratePlanAsync(GeneratePlanRequestDto request, CancellationToken cancellationToken = default);
}

