using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string BillingCycle { get; set; } = "monthly"; // monthly | yearly | lifetime

    /// <summary>Tier phân loại gói: "free" | "premium" | "vip"</summary>
    public string Tier { get; set; } = "free";

    /// <summary>Giới hạn lượt tạo AI plan. Null = không giới hạn (VIP).</summary>
    public int? AiRequestsLimit { get; set; }

    /// <summary>Giới hạn lượt refine AI plan. Null = không giới hạn (VIP).</summary>
    public int? AiRefineLimit { get; set; }

    public int? StorageLimitMb { get; set; }

    /// <summary>Số plan tối đa được lưu cùng lúc. Null = không giới hạn (VIP).</summary>
    public int? MaxPlans { get; set; }

    public string? Features { get; set; } // JSON or serialized metadata
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
