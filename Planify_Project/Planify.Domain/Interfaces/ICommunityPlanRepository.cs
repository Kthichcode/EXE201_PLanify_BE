using Planify.Domain.Entities;

namespace Planify.Domain.Interfaces;

/// <summary>
/// Repository interface cho CommunityPlan entity.
/// Implementation: Infrastructure/Repositories/CommunityPlanRepository.cs
/// </summary>
public interface ICommunityPlanRepository
{
    Task<CommunityPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load kèm Plan (với Tasks) và Likes.</summary>
    Task<CommunityPlan?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load kèm Plan (với Tasks) cho admin — không giới hạn status.</summary>
    Task<CommunityPlan?> GetByIdWithDetailsForAdminAsync(Guid id, CancellationToken ct = default);


    /// <summary>Kiểm tra plan gốc đã được publish chưa.</summary>
    Task<bool> ExistsByPlanIdAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Danh sách approved plans có phân trang, tìm kiếm, lọc category.</summary>
    Task<(List<CommunityPlan> Items, int TotalCount)> GetApprovedPagedAsync(
        string? search,
        Guid? categoryId,
        string? sortBy,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Danh sách pending plans (admin).</summary>
    Task<(List<CommunityPlan> Items, int TotalCount)> GetPendingPagedAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Danh sách community plans của 1 user.</summary>
    Task<List<CommunityPlan>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Kiểm tra user đã like chưa.</summary>
    Task<CommunityPlanLike?> GetLikeAsync(Guid communityPlanId, Guid userId, CancellationToken ct = default);

    Task AddAsync(CommunityPlan communityPlan, CancellationToken ct = default);
    Task AddLikeAsync(CommunityPlanLike like, CancellationToken ct = default);
    Task AddCopyAsync(PlanCopy copy, CancellationToken ct = default);
    Task RemoveLikeAsync(CommunityPlanLike like, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}
