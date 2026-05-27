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
    public int AiRequestsUsed { get; set; } = 0;
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual SubscriptionPlan? Plan { get; set; }
}
