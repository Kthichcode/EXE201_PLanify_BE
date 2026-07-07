using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Feedback;

public class SubmitGeneralFeedbackDto
{
    /// <summary>
    /// Hạng mục phản hồi.
    /// Giá trị hợp lệ: ai_quality | ui_ux | performance | bug_report |
    /// feature_request | content | subscription | general
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "general";

    /// <summary>Tiêu đề ngắn gọn (tối đa 255 ký tự).</summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết vấn đề / đề xuất.</summary>
    [MaxLength(3000)]
    public string? Description { get; set; }

    /// <summary>Điểm đánh giá từ 1 đến 5 (tuỳ chọn).</summary>
    [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.")]
    public int? Rating { get; set; }
}
