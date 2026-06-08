using System;

namespace Planify.Application.DTOs.Subscriptions;

public class UpgradeSubscriptionResultDto
{
    public UserSubscriptionDto Subscription { get; set; } = null!;
    public string? PaymentUrl { get; set; }
}
