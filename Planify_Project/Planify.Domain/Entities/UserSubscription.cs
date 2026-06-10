using System;

namespace Planify.Domain.Entities;

public class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "active"; // active | expired | cancelled | pending
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Số lượt tạo AI plan đã sử dụng trong kỳ hiện tại.</summary>
    public int AiRequestsUsed { get; set; } = 0;

    /// <summary>Số lượt refine AI plan đã sử dụng trong kỳ hiện tại.</summary>
    public int AiRefineUsed { get; set; } = 0;

    /// <summary>
    /// Ngày UTC cuối cùng Premium được cộng thêm 1 lượt refine (daily refill).
    /// Chỉ áp dụng khi user đã hết lượt (AiRefineUsed >= plan.AiRefineLimit).
    /// </summary>
    public DateTime? LastRefillAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual SubscriptionPlan? Plan { get; set; }
}
