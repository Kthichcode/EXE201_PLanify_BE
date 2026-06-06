using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class CommunityPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Plan gốc được publish lên library (unique).</summary>
    public Guid PlanId { get; set; }

    /// <summary>Người publish plan lên library.</summary>
    public Guid UserId { get; set; }

    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>pending | approved | rejected</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Admin đã duyệt/từ chối.</summary>
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }

    public int DownloadCount { get; set; } = 0;

    /// <summary>Denormalized cache — tổng số like, tăng hiệu năng đọc.</summary>
    public int LikeCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Plan? Plan { get; set; }
    public virtual ICollection<CommunityPlanLike> Likes { get; set; } = new List<CommunityPlanLike>();
    public virtual ICollection<PlanCopy> Copies { get; set; } = new List<PlanCopy>();
}
