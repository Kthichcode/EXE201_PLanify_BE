using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Feedback;

public class SubmitFeedbackDto
{
    [Required]
    public Guid PlanId { get; set; }

    /// <summary>Community plan liên kết (tuỳ chọn — điền nếu đã publish).</summary>
    public Guid? CommunityPlanId { get; set; }

    /// <summary>AI plan có hiệu quả không?</summary>
    [Required]
    public bool IsEffective { get; set; }

    /// <summary>Lý do nếu không hiệu quả (bắt buộc khi IsEffective = false).</summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }

    /// <summary>Đề xuất cải thiện từ người dùng.</summary>
    [MaxLength(2000)]
    public string? Suggestions { get; set; }

    /// <summary>Điểm đánh giá từ 1 đến 5.</summary>
    [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.")]
    public int? Rating { get; set; }
}
