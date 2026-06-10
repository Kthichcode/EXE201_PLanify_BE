using System;

namespace Planify.Application.DTOs.Subscriptions;

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string BillingCycle { get; set; } = string.Empty;
    /// <summary>Tier gói: "free" | "premium" | "vip"</summary>
    public string Tier { get; set; } = "free";
    /// <summary>Giới hạn lượt tạo AI plan. Null = không giới hạn.</summary>
    public int? AiRequestsLimit { get; set; }
    /// <summary>Giới hạn lượt refine AI plan. Null = không giới hạn.</summary>
    public int? AiRefineLimit { get; set; }
    public int? StorageLimitMb { get; set; }
    public int? MaxPlans { get; set; }
    public string? Features { get; set; }
    public bool IsActive { get; set; }
}
