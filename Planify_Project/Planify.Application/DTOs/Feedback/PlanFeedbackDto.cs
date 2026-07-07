using System;

namespace Planify.Application.DTOs.Feedback;

public class PlanFeedbackDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CommunityPlanId { get; set; }

    public bool IsEffective { get; set; }
    public string? Reason { get; set; }
    public string? Suggestions { get; set; }
    public int? Rating { get; set; }

    /// <summary>Tên kế hoạch (denormalized để admin đọc nhanh).</summary>
    public string? PlanTitle { get; set; }

    /// <summary>Email hoặc tên user gửi feedback (denormalized).</summary>
    public string? UserName { get; set; }

    public DateTime CreatedAt { get; set; }
}
