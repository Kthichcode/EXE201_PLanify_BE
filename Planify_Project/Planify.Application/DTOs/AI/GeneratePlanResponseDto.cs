using System.Text.Json.Nodes;

namespace Planify.Application.DTOs.AI;

/// <summary>
/// Kết quả thô từ AI khi generate plan.
/// rawJson chứa toàn bộ JSON string mà AI trả về để caller tự parse hoặc forward về frontend.
/// </summary>
public class GeneratePlanResponseDto
{
    /// <summary>JSON string nguyên bản từ AI (đã được validate là JSON hợp lệ)</summary>
    public JsonObject PlanData { get; set; } = new();

    /// <summary>Câu message ngắn cho user (trích từ metadata.message)</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Model đã dùng</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Thời gian AI xử lý (ms)</summary>
    public long ElapsedMs { get; set; }
}
