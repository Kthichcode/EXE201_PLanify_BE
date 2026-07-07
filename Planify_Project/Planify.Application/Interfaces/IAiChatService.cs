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
    /// <param name="request">Yêu cầu của user.</param>
    /// <param name="templateContext">
    /// (Optional) Nội dung template được format sẵn để inject vào system prompt.
    /// Nếu null, AI tự tạo kế hoạch không dựa template.
    /// </param>
    /// <param name="cancellationToken"/>
    Task<GeneratePlanResponseDto> GeneratePlanAsync(
        GeneratePlanRequestDto request,
        string? templateContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chỉnh sửa kế hoạch đã tạo dựa trên yêu cầu của người dùng.
    /// Nhận JSON plan hiện tại + instruction → trả về JSON plan đã được chỉnh sửa (cùng schema).
    /// </summary>
    Task<GeneratePlanResponseDto> RefinePlanAsync(string currentPlanJson, string instruction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân tích tình trạng trễ tiến độ của plan và đề xuất tối ưu lại lịch trình.
    /// AI tự chọn chiến lược phù hợp:
    ///   - "reschedule": dồn task trễ sang ngày khác, không thay đổi deadline chung.
    ///   - "extend_deadline": đề xuất mở rộng deadline khi deadline đang đến gần.
    /// Trả về JSON plan đề xuất (chưa lưu DB) + strategy + giải thích.
    /// </summary>
    Task<GeneratePlanResponseDto> AnalyzeDelayAsync(string currentPlanJson, int overdueCount, int daysToDeadline, CancellationToken cancellationToken = default);
}
