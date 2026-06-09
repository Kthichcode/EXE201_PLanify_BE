using System;

namespace Planify.Domain.Entities;

public class PlanCopy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommunityPlanId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Plan mới được tạo ra sau khi copy về tài khoản người dùng.</summary>
    public Guid NewPlanId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual CommunityPlan? CommunityPlan { get; set; }
    public virtual Plan? NewPlan { get; set; }
}
