using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;

namespace Planify.Application.Services;

/// <summary>
/// Triển khai ISubscriptionGuardService.
/// Enforce giới hạn theo tier trước khi thực hiện hành động.
///
/// Quy tắc cứng theo tier (không admin chỉnh được):
///   - free:    Chỉ xem thư viện, không copy/publish.
///   - premium: Xem + copy + publish thư viện.
///   - vip:     Toàn quyền, không giới hạn lượt.
///
/// Quy tắc mềm (admin chỉnh được qua SubscriptionPlan):
///   - AiRequestsLimit: giới hạn lượt tạo AI plan.
///   - AiRefineLimit:   giới hạn lượt refine AI plan.
///   - MaxPlans:        số plan active tối đa.
/// </summary>
public class SubscriptionGuardService : ISubscriptionGuardService
{
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IPlanRepository _planRepo;

    // Tier strings — hardcoded constants
    private const string TierFree    = "free";
    private const string TierPremium = "premium";
    private const string TierVip     = "vip";

    public SubscriptionGuardService(
        ISubscriptionRepository subscriptionRepo,
        IPlanRepository planRepo)
    {
        _subscriptionRepo = subscriptionRepo;
        _planRepo          = planRepo;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task EnforceAndConsumeAiGenerateAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await GetOrCreateFreeSubAsync(userId, ct);

        // VIP hoặc unlimited (limit = null) → bỏ qua
        if (IsVip(sub) || sub.Plan!.AiRequestsLimit == null)
            return;

        if (sub.AiRequestsUsed >= sub.Plan.AiRequestsLimit.Value)
        {
            throw new InvalidOperationException(
                $"Bạn đã hết {sub.Plan.AiRequestsLimit} lượt tạo kế hoạch AI của gói {sub.Plan.Name}. " +
                "Vui lòng nâng cấp gói để tiếp tục.");
        }

        sub.AiRequestsUsed++;
        sub.UpdatedAt = DateTime.UtcNow;
        await _subscriptionRepo.SaveChangesAsync(ct);
    }

    public async Task EnforceAndConsumeAiRefineAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await GetOrCreateFreeSubAsync(userId, ct);

        // VIP hoặc unlimited → bỏ qua
        if (IsVip(sub) || sub.Plan!.AiRefineLimit == null)
            return;

        // ── Daily refill logic cho Premium ───────────────────────────────────
        // Chỉ khi đã hết lượt (AiRefineUsed >= limit) mới xét refill.
        // Mỗi ngày UTC, nếu LastRefillAt chưa phải hôm nay → cộng thêm 1 lượt.
        if (sub.AiRefineUsed >= sub.Plan.AiRefineLimit.Value && IsPremium(sub))
        {
            var today = DateTime.UtcNow.Date;
            if (!sub.LastRefillAt.HasValue || sub.LastRefillAt.Value.Date < today)
            {
                // Cộng thêm 1 lượt: giảm counter đi 1 (tối thiểu 0)
                sub.AiRefineUsed = Math.Max(0, sub.AiRefineUsed - 1);
                sub.LastRefillAt = DateTime.UtcNow;
                sub.UpdatedAt    = DateTime.UtcNow;
                await _subscriptionRepo.SaveChangesAsync(ct);
            }
        }

        // Kiểm tra lại sau khi refill
        if (sub.AiRefineUsed >= sub.Plan.AiRefineLimit.Value)
        {
            var refillMsg = IsPremium(sub)
                ? " Mỗi ngày bạn được cộng thêm 1 lượt sau khi hết hạn mức."
                : string.Empty;

            throw new InvalidOperationException(
                $"Bạn đã hết {sub.Plan.AiRefineLimit} lượt chỉnh sửa kế hoạch AI của gói {sub.Plan.Name}." +
                refillMsg +
                " Vui lòng nâng cấp gói để có thêm lượt.");
        }

        sub.AiRefineUsed++;
        sub.UpdatedAt = DateTime.UtcNow;
        await _subscriptionRepo.SaveChangesAsync(ct);
    }

    public async Task EnforceMaxPlansAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await GetOrCreateFreeSubAsync(userId, ct);

        // VIP hoặc unlimited → bỏ qua
        if (IsVip(sub) || sub.Plan!.MaxPlans == null)
            return;

        // Chỉ đếm plan active (không tính draft/discarded)
        var activeCount = await _planRepo.CountActiveByUserIdAsync(userId, ct);

        if (activeCount >= sub.Plan.MaxPlans.Value)
        {
            throw new InvalidOperationException(
                $"Gói {sub.Plan.Name} chỉ cho phép lưu tối đa {sub.Plan.MaxPlans} kế hoạch cùng lúc. " +
                "Vui lòng xóa bớt kế hoạch hoặc nâng cấp gói để lưu thêm.");
        }
    }

    public async Task EnforceCommunityPlanCopyAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await GetUserTierAsync(userId, ct);

        if (tier == TierFree)
        {
            throw new UnauthorizedAccessException(
                "Gói Free chỉ cho phép xem thư viện kế hoạch. " +
                "Nâng cấp lên Premium hoặc VIP để lưu kế hoạch từ thư viện về tài khoản của bạn.");
        }
    }

    public async Task EnforcePublishPlanAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await GetUserTierAsync(userId, ct);

        if (tier == TierFree)
        {
            throw new UnauthorizedAccessException(
                "Gói Free không có quyền chia sẻ kế hoạch lên thư viện cộng đồng. " +
                "Nâng cấp lên Premium hoặc VIP để sử dụng tính năng này.");
        }
    }

    public async Task<string> GetUserTierAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await GetOrCreateFreeSubAsync(userId, ct);
        return sub.Plan?.Tier?.ToLowerInvariant() ?? TierFree;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Lấy subscription active của user. Nếu chưa có, tạo subscription Free mặc định.
    /// </summary>
    private async Task<UserSubscription> GetOrCreateFreeSubAsync(Guid userId, CancellationToken ct)
    {
        var sub = await _subscriptionRepo.GetActiveUserSubscriptionAsync(userId, ct);

        if (sub == null)
        {
            // Tự động gán gói Free nếu user chưa có subscription
            var freePlan = await _subscriptionRepo.GetPlanByNameAsync("free", ct)
                ?? await _subscriptionRepo.GetPlanByNameAsync("Free", ct);

            if (freePlan == null)
                throw new InvalidOperationException(
                    "Không tìm thấy gói Free trong hệ thống. Vui lòng liên hệ quản trị viên.");

            sub = new UserSubscription
            {
                UserId         = userId,
                PlanId         = freePlan.Id,
                Status         = "active",
                StartedAt      = DateTime.UtcNow,
                AiRequestsUsed = 0,
                AiRefineUsed   = 0,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
                Plan           = freePlan
            };

            await _subscriptionRepo.AddUserSubscriptionAsync(sub, ct);
            await _subscriptionRepo.SaveChangesAsync(ct);
        }

        return sub;
    }

    private static bool IsVip(UserSubscription sub)
        => sub.Plan?.Tier?.Equals(TierVip, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsPremium(UserSubscription sub)
        => sub.Plan?.Tier?.Equals(TierPremium, StringComparison.OrdinalIgnoreCase) == true;
}
