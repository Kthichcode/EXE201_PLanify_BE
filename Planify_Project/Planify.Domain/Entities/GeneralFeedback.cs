using System;

namespace Planify.Domain.Entities;

/// <summary>
/// Phản hồi tổng quát từ người dùng — có thể gửi bất kỳ lúc nào.
/// Phân theo hạng mục để admin dễ phân loại và xử lý.
/// </summary>
public class GeneralFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Người dùng gửi feedback.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hạng mục: ai_quality | ui_ux | performance | bug_report |
    /// feature_request | content | subscription | general
    /// </summary>
    public string Category { get; set; } = "general";

    /// <summary>Tiêu đề ngắn gọn mô tả vấn đề.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết.</summary>
    public string? Description { get; set; }

    /// <summary>Điểm đánh giá 1–5 (tuỳ chọn).</summary>
    public int? Rating { get; set; }

    /// <summary>
    /// Trạng thái xử lý của admin: pending | reviewed | resolved
    /// </summary>
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
