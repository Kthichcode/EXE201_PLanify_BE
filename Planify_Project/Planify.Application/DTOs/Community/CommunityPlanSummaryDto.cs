using System;

namespace Planify.Application.DTOs.Community;

/// <summary>
/// Rút gọn — dùng cho danh sách thư viện (không kèm tasks).
/// </summary>
public class CommunityPlanSummaryDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid UserId { get; set; }
    public string? AuthorName { get; set; }
    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;

    public int DownloadCount { get; set; }
    public int LikeCount { get; set; }
    public bool? IsLikedByCurrentUser { get; set; }

    public DateTime CreatedAt { get; set; }
}
