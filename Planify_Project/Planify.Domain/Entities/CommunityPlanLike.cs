using System;

namespace Planify.Domain.Entities;

public class CommunityPlanLike
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CommunityPlanId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual CommunityPlan? CommunityPlan { get; set; }
}
