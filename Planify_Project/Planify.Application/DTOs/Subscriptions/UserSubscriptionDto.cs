using System;

namespace Planify.Application.DTOs.Subscriptions;

public class UserSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;

    /// <summary>Tier gói: "free" | "premium" | "vip"</summary>
    public string Tier { get; set; } = "free";

    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // ── AI Generate ───────────────────────────────────────────────────────────
    public int AiRequestsUsed { get; set; }
    public int? AiRequestsLimit { get; set; }

    /// <summary>null = không giới hạn (VIP)</summary>
    public int? RemainingAiRequests { get; set; }

    // ── AI Refine ─────────────────────────────────────────────────────────────
    public int AiRefineUsed { get; set; }
    public int? AiRefineLimit { get; set; }

    /// <summary>null = không giới hạn (VIP)</summary>
    public int? RemainingAiRefines { get; set; }

    // ── Plans storage ─────────────────────────────────────────────────────────
    public int? StorageLimitMb { get; set; }
    public int? MaxPlans { get; set; }

    // ── Hardcoded capability flags (theo tier) ────────────────────────────────
    /// <summary>Free = false; Premium/VIP = true</summary>
    public bool CanCopyFromLibrary { get; set; }

    /// <summary>Free = false; Premium/VIP = true</summary>
    public bool CanPublishToLibrary { get; set; }

    public DateTime? CancelledAt { get; set; }
}
