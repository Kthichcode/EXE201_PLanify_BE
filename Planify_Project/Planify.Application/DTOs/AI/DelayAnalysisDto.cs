using System;
using System.Text.Json.Nodes;

namespace Planify.Application.DTOs.AI;

/// <summary>Request phân tích tình trạng trễ tiến độ của một plan.</summary>
public class AnalyzeDelayRequestDto
{
    public Guid PlanId { get; set; }

    /// <summary>
    /// Nếu FE truyền vào, BE sẽ ép AI dùng strategy này thay vì tự chọn.
    /// Giá trị hợp lệ: "reschedule" | "extend_deadline"
    /// Null = để AI tự quyết định.
    /// </summary>
    public string? ForceStrategy { get; set; }
}

/// <summary>
/// Kết quả AI phân tích tình trạng trễ — chỉ là đề xuất, chưa apply vào DB.
/// FE hiển thị cho user xác nhận trước.
/// </summary>
public class AnalyzeDelayResponseDto
{
    /// <summary>JSON plan đề xuất từ AI (chưa lưu DB).</summary>
    public JsonObject? ProposedPlanData { get; set; }

    /// <summary>Giải thích của AI về tình huống và hướng giải quyết.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Chiến lược AI chọn:
    /// "reschedule" — dồn task sang ngày khác, không đổi deadline chung.
    /// "extend_deadline" — đề xuất mở rộng deadline tổng.
    /// </summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>Số subtask đang trễ.</summary>
    public int OverdueCount { get; set; }

    /// <summary>Số ngày còn lại đến deadline chung của plan (âm = đã quá hạn).</summary>
    public int DaysToDeadline { get; set; }

    public string Model { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
}

/// <summary>Request apply đề xuất AI đã được user xác nhận vào DB.</summary>
public class ApplyDelayFixRequestDto
{
    public Guid PlanId { get; set; }

    /// <summary>JSON plan đề xuất từ AI (lấy từ ProposedPlanData của AnalyzeDelayResponseDto).</summary>
    public JsonObject PlanData { get; set; } = new();
}
