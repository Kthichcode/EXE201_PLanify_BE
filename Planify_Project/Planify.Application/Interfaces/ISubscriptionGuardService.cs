namespace Planify.Application.Interfaces;

/// <summary>
/// Service kiểm tra và enforce giới hạn subscription trước khi thực hiện hành động.
/// Tập trung toàn bộ logic ràng buộc theo tier (free/premium/vip).
/// </summary>
public interface ISubscriptionGuardService
{
    /// <summary>
    /// Kiểm tra và tiêu thụ 1 lượt tạo AI plan.
    /// Throws <see cref="InvalidOperationException"/> nếu đã hết lượt.
    /// </summary>
    Task EnforceAndConsumeAiGenerateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra và tiêu thụ 1 lượt refine AI plan.
    /// Với Premium: nếu đã hết lượt, mỗi ngày được cộng thêm 1 lượt trước khi check.
    /// Throws <see cref="InvalidOperationException"/> nếu đã hết lượt.
    /// </summary>
    Task EnforceAndConsumeAiRefineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra user có thể lưu thêm plan không (giới hạn MaxPlans).
    /// Chỉ đếm plan active (không tính draft).
    /// Throws <see cref="InvalidOperationException"/> nếu đã đạt giới hạn.
    /// </summary>
    Task EnforceMaxPlansAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra user có quyền copy/lưu plan từ thư viện cộng đồng (Premium+ only).
    /// Throws <see cref="UnauthorizedAccessException"/> nếu tier không đủ.
    /// </summary>
    Task EnforceCommunityPlanCopyAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra user có quyền publish plan lên thư viện cộng đồng (Premium+ only).
    /// Throws <see cref="UnauthorizedAccessException"/> nếu tier không đủ.
    /// </summary>
    Task EnforcePublishPlanAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Lấy tier hiện tại của user: "free" | "premium" | "vip".</summary>
    Task<string> GetUserTierAsync(Guid userId, CancellationToken ct = default);
}
