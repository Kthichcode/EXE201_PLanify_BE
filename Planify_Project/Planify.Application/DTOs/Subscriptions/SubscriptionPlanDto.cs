using System;

namespace Planify.Application.DTOs.Subscriptions;

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string BillingCycle { get; set; } = string.Empty;
    public int? AiRequestsLimit { get; set; }
    public int? StorageLimitMb { get; set; }
    public int? MaxPlans { get; set; }
    public string? Features { get; set; }
    public bool IsActive { get; set; }
}
