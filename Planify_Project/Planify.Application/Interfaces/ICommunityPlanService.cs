using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Community;
using Planify.Application.DTOs.Plans;
using System;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface ICommunityPlanService
{
    // ── User actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Publish plan của user lên community library.
    /// Plan phải thuộc user, status = "active", chưa được publish.
    /// CommunityPlan mới sẽ có status = "pending" — chờ admin duyệt.
    /// </summary>
    Task<CommunityPlanDto> PublishPlanAsync(PublishPlanDto dto, Guid userId);

    /// <summary>Duyệt thư viện — chỉ trả về plan "approved", hỗ trợ tìm kiếm và phân trang.</summary>
    Task<PagedResultDto<CommunityPlanSummaryDto>> GetLibraryAsync(CommunityPlanQueryDto query, Guid? currentUserId);

    /// <summary>Xem chi tiết 1 community plan (kèm tasks của plan gốc).</summary>
    Task<CommunityPlanDto?> GetCommunityPlanByIdAsync(Guid id, Guid? currentUserId);

    /// <summary>
    /// Deep-clone plan gốc (Plan + Tasks + SubTasks) về tài khoản của user.
    /// Tăng DownloadCount. Tạo record PlanCopy.
    /// </summary>
    Task<PlanDto> CopyPlanToUserAsync(Guid communityPlanId, Guid userId);

    /// <summary>
    /// Toggle like: nếu đã like → unlike; chưa like → like.
    /// Cập nhật LikeCount trên CommunityPlan.
    /// </summary>
    Task<bool> ToggleLikeAsync(Guid communityPlanId, Guid userId);

    /// <summary>Gỡ plan của mình khỏi library (chỉ owner hoặc admin).</summary>
    Task UnpublishPlanAsync(Guid communityPlanId, Guid userId, bool isAdmin = false);

    /// <summary>Danh sách community plans mà user đã publish.</summary>
    Task<List<CommunityPlanSummaryDto>> GetMyPublishedPlansAsync(Guid userId);

    // ── Admin actions ───────────────────────────────────────────────────────

    /// <summary>Danh sách community plans chờ admin duyệt.</summary>
    Task<PagedResultDto<CommunityPlanSummaryDto>> GetPendingPlansAsync(int page, int pageSize);

    /// <summary>Admin duyệt plan → status = "approved".</summary>
    Task<CommunityPlanDto> ApprovePlanAsync(Guid id, Guid adminId);

    /// <summary>Admin từ chối plan → status = "rejected" kèm lý do.</summary>
    Task<CommunityPlanDto> RejectPlanAsync(Guid id, Guid adminId, string reason);
}
