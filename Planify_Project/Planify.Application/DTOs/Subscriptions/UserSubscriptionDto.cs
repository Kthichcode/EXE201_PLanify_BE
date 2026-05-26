using System;

namespace Planify.Application.DTOs.Subscriptions;

public class UserSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int AiRequestsUsed { get; set; }
    public int? AiRequestsLimit { get; set; }
    public int? RemainingAiRequests { get; set; } // null represents unlimited
    public int? StorageLimitMb { get; set; }
    public int? MaxPlans { get; set; }
    public DateTime? CancelledAt { get; set; }
}
