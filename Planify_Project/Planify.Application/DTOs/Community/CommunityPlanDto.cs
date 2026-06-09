using System;
using Planify.Application.DTOs.Plans;

namespace Planify.Application.DTOs.Community;

public class CommunityPlanDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid UserId { get; set; }
    public string? AuthorName { get; set; }
    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }

    public int DownloadCount { get; set; }
    public int LikeCount { get; set; }

    /// <summary>True nếu user hiện tại đã like (null khi anonymous).</summary>
    public bool? IsLikedByCurrentUser { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Chi tiết plan gốc kèm tasks (dùng khi xem detail).</summary>
    public PlanDto? Plan { get; set; }
}
