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
    public int? AiRequestsLimit { get; set; }
    public int? StorageLimitMb { get; set; }
    public int? MaxPlans { get; set; }
    public string? Features { get; set; } // JSON or serialized metadata
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
