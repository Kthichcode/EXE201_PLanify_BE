using System;

namespace Planify.Domain.Entities;

/// <summary>
/// Lưu phản hồi của người dùng về hiệu quả của kế hoạch (plan) được tạo bởi AI.
/// Được submit sau khi publish plan lên thư viện cộng đồng.
/// Admin có thể xem để cải thiện chất lượng AI.
/// </summary>
public class PlanFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Plan gốc mà feedback này thuộc về.</summary>
    public Guid PlanId { get; set; }

    /// <summary>Người dùng gửi feedback.</summary>
    public Guid UserId { get; set; }

    /// <summary>Community plan được publish (nếu có).</summary>
    public Guid? CommunityPlanId { get; set; }

    /// <summary>AI plan có hiệu quả không? true = có, false = không.</summary>
    public bool IsEffective { get; set; }

    /// <summary>Lý do nếu plan không hiệu quả.</summary>
    public string? Reason { get; set; }

    /// <summary>Đề xuất cải thiện từ người dùng.</summary>
    public string? Suggestions { get; set; }

    /// <summary>Điểm đánh giá 1–5 (tuỳ chọn).</summary>
    public int? Rating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Plan? Plan { get; set; }
    public virtual CommunityPlan? CommunityPlan { get; set; }
}
