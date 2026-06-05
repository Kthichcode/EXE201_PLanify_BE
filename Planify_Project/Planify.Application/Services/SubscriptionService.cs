using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Subscriptions;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;

namespace Planify.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repo;
    private readonly IPaymentService _paymentService;

    public SubscriptionService(ISubscriptionRepository repo, IPaymentService paymentService)
    {
        _repo = repo;
        _paymentService = paymentService;
    }

    public async Task<ResponseDto<IEnumerable<SubscriptionPlanDto>>> GetActivePlansAsync()
    {
        var plans = await _repo.GetActivePlansAsync();
        return ResponseDto<IEnumerable<SubscriptionPlanDto>>.Success(
            plans.Select(MapToPlanDto),
            "Lấy danh sách gói dịch vụ thành công.");
    }

    public async Task<ResponseDto<UserSubscriptionDto>> GetUserSubscriptionAsync(Guid userId)
    {
        var currentSub = await _repo.GetActiveUserSubscriptionAsync(userId);

        if (currentSub == null)
        {
            var freePlan = await _repo.GetPlanByNameAsync("free");
            if (freePlan != null)
            {
                currentSub = new UserSubscription
                {
                    UserId         = userId,
                    PlanId         = freePlan.Id,
                    Status         = "active",
                    StartedAt      = DateTime.UtcNow,
                    ExpiresAt      = null,
                    AiRequestsUsed = 0,
                    CreatedAt      = DateTime.UtcNow,
                    UpdatedAt      = DateTime.UtcNow,
                    Plan           = freePlan
                };
                await _repo.AddUserSubscriptionAsync(currentSub);
                await _repo.SaveChangesAsync();
            }
            else
            {
                return ResponseDto<UserSubscriptionDto>.Fail(
                    "Không tìm thấy gói hiện tại của người dùng và không có gói Free mặc định nào được cấu hình.", 404);
            }
        }
        else if (currentSub.ExpiresAt.HasValue && currentSub.ExpiresAt.Value < DateTime.UtcNow)
        {
            currentSub.Status    = "expired";
            currentSub.UpdatedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync();
            return await GetUserSubscriptionAsync(userId);
        }

        return ResponseDto<UserSubscriptionDto>.Success(
            MapToUserSubscriptionDto(currentSub),
            "Lấy thông tin gói sử dụng thành công.");
    }

    public async Task<ResponseDto<UpgradeSubscriptionResultDto>> UpgradeSubscriptionAsync(Guid userId, UpgradeSubscriptionRequestDto dto)
    {
        var plan = await _repo.GetPlanByIdAsync(dto.PlanId);
        if (plan == null || !plan.IsActive)
            return ResponseDto<UpgradeSubscriptionResultDto>.Fail("Gói dịch vụ không tồn tại hoặc đã bị ngừng hoạt động.", 404);

        if (dto.PaymentMethod.Equals("PayOS", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(dto.ReturnUrl) || string.IsNullOrEmpty(dto.CancelUrl))
            {
                return ResponseDto<UpgradeSubscriptionResultDto>.Fail("ReturnUrl và CancelUrl là bắt buộc khi chọn thanh toán qua PayOS.", 400);
            }

            // 1. Generate unique order code
            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L + Random.Shared.Next(0, 1000);

            // 2. Create pending UserSubscription
            var newSub = new UserSubscription
            {
                UserId         = userId,
                PlanId         = plan.Id,
                Status         = "pending",
                StartedAt      = DateTime.UtcNow,
                ExpiresAt      = null, // Will be set when payment succeeds
                AiRequestsUsed = 0,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };

            await _repo.AddUserSubscriptionAsync(newSub);
            await _repo.SaveChangesAsync();

            // 3. Create pending PaymentTransaction
            var txn = new PaymentTransaction
            {
                UserId         = userId,
                SubscriptionId = newSub.Id,
                Amount         = plan.Price,
                Currency       = "VND",
                Status         = "pending",
                PaymentMethod  = "PayOS",
                PaymentRef     = orderCode.ToString(),
                PaidAt         = null,
                CreatedAt      = DateTime.UtcNow
            };

            await _repo.AddPaymentTransactionAsync(txn);
            await _repo.SaveChangesAsync();

            // 4. Call PayOS API to get Payment Url
            string checkoutUrl;
            try
            {
                checkoutUrl = await _paymentService.CreatePaymentLinkAsync(
                    orderCode,
                    plan.Price,
                    $"Planify {plan.Name}",
                    dto.ReturnUrl,
                    dto.CancelUrl
                );
            }
            catch (Exception ex)
            {
                newSub.Status = "cancelled";
                txn.Status = "failed";
                await _repo.SaveChangesAsync();
                return ResponseDto<UpgradeSubscriptionResultDto>.Fail($"Lỗi khi tạo liên kết thanh toán PayOS: {ex.Message}", 500);
            }

            return ResponseDto<UpgradeSubscriptionResultDto>.Success(new UpgradeSubscriptionResultDto
            {
                Subscription = MapToUserSubscriptionDto(newSub),
                PaymentUrl = checkoutUrl
            }, "Tạo liên kết thanh toán thành công.");
        }
        else
        {
            var activeSubs = await _repo.GetActiveUserSubscriptionsAsync(userId);
            foreach (var sub in activeSubs)
            {
                sub.Status      = "cancelled";
                sub.CancelledAt = DateTime.UtcNow;
                sub.UpdatedAt   = DateTime.UtcNow;
            }

            DateTime? expiresAt = plan.BillingCycle.ToLower() switch
            {
                "monthly" => DateTime.UtcNow.AddMonths(1),
                "yearly"  => DateTime.UtcNow.AddYears(1),
                _         => null
            };

            var newSub = new UserSubscription
            {
                UserId         = userId,
                PlanId         = plan.Id,
                Status         = "active",
                StartedAt      = DateTime.UtcNow,
                ExpiresAt      = expiresAt,
                AiRequestsUsed = 0,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
                Plan           = plan
            };

            await _repo.AddUserSubscriptionAsync(newSub);
            await _repo.SaveChangesAsync();

            var txn = new PaymentTransaction
            {
                UserId         = userId,
                SubscriptionId = newSub.Id,
                Amount         = plan.Price,
                Currency       = "VND",
                Status         = "success",
                PaymentMethod  = dto.PaymentMethod,
                PaymentRef     = "TXN_" + Guid.NewGuid().ToString("N")[..12].ToUpper(),
                PaidAt         = DateTime.UtcNow,
                CreatedAt      = DateTime.UtcNow
            };

            await _repo.AddPaymentTransactionAsync(txn);
            await _repo.SaveChangesAsync();

            return ResponseDto<UpgradeSubscriptionResultDto>.Success(new UpgradeSubscriptionResultDto
            {
                Subscription = MapToUserSubscriptionDto(newSub),
                PaymentUrl = null
            }, "Nâng cấp gói dịch vụ thành công (Simulation).");
        }
    }

    public async Task<ResponseDto<bool>> ConfirmPaymentAsync(long orderCode, string status, string paymentRef, CancellationToken ct = default)
    {
        var txn = await _repo.GetPaymentTransactionByRefAsync(orderCode.ToString(), ct);
        if (txn == null)
            return ResponseDto<bool>.Fail("Không tìm thấy giao dịch tương ứng.", 404);

        if (txn.Status == "success")
            return ResponseDto<bool>.Success(true, "Giao dịch đã được xử lý trước đó.");

        if (status.Equals("PAID", StringComparison.OrdinalIgnoreCase) || status.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            txn.Status = "success";
            txn.PaidAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(paymentRef))
            {
                txn.PaymentRef = paymentRef;
            }

            var sub = txn.Subscription;
            if (sub != null)
            {
                sub.Status = "active";
                sub.StartedAt = DateTime.UtcNow;

                var plan = await _repo.GetPlanByIdAsync(sub.PlanId, ct);
                if (plan != null)
                {
                    sub.ExpiresAt = plan.BillingCycle.ToLower() switch
                    {
                        "monthly" => DateTime.UtcNow.AddMonths(1),
                        "yearly"  => DateTime.UtcNow.AddYears(1),
                        _         => null
                    };
                }
                sub.UpdatedAt = DateTime.UtcNow;

                // Deactivate other active subscriptions
                var activeSubs = await _repo.GetActiveUserSubscriptionsAsync(txn.UserId, ct);
                foreach (var otherSub in activeSubs)
                {
                    if (otherSub.Id != sub.Id)
                    {
                        otherSub.Status = "cancelled";
                        otherSub.CancelledAt = DateTime.UtcNow;
                        otherSub.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }
        else if (status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) || status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            txn.Status = "failed";

            var sub = txn.Subscription;
            if (sub != null)
            {
                sub.Status = "cancelled";
                sub.CancelledAt = DateTime.UtcNow;
                sub.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _repo.SaveChangesAsync(ct);
        return ResponseDto<bool>.Success(true, "Cập nhật trạng thái giao dịch thành công.");
    }

    public async Task<ResponseDto<IEnumerable<SubscriptionPlanDto>>> GetAllPlansAsync()
    {
        var plans = await _repo.GetAllPlansAsync();
        return ResponseDto<IEnumerable<SubscriptionPlanDto>>.Success(
            plans.Select(MapToPlanDto),
            "Lấy toàn bộ danh sách gói thành công.");
    }

    public async Task<ResponseDto<SubscriptionPlanDto>> CreatePlanAsync(CreateSubscriptionPlanDto dto)
    {
        if (await _repo.PlanNameExistsAsync(dto.Name))
            return ResponseDto<SubscriptionPlanDto>.Fail("Tên gói dịch vụ đã tồn tại.", 400);

        var plan = new SubscriptionPlan
        {
            Name            = dto.Name,
            Description     = dto.Description,
            Price           = dto.Price,
            BillingCycle    = dto.BillingCycle,
            AiRequestsLimit = dto.AiRequestsLimit,
            StorageLimitMb  = dto.StorageLimitMb,
            MaxPlans        = dto.MaxPlans,
            Features        = dto.Features,
            IsActive        = dto.IsActive,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };

        await _repo.AddPlanAsync(plan);
        await _repo.SaveChangesAsync();

        return ResponseDto<SubscriptionPlanDto>.Success(MapToPlanDto(plan), "Tạo gói dịch vụ mới thành công.", 201);
    }

    public async Task<ResponseDto<SubscriptionPlanDto>> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanDto dto)
    {
        var plan = await _repo.GetPlanByIdAsync(id);
        if (plan == null)
            return ResponseDto<SubscriptionPlanDto>.Fail("Không tìm thấy gói dịch vụ cần chỉnh sửa.", 404);

        if (await _repo.PlanNameExistsAsync(dto.Name, excludeId: id))
            return ResponseDto<SubscriptionPlanDto>.Fail("Tên gói dịch vụ đã được sử dụng ở gói khác.", 400);

        plan.Name            = dto.Name;
        plan.Description     = dto.Description;
        plan.Price           = dto.Price;
        plan.BillingCycle    = dto.BillingCycle;
        plan.AiRequestsLimit = dto.AiRequestsLimit;
        plan.StorageLimitMb  = dto.StorageLimitMb;
        plan.MaxPlans        = dto.MaxPlans;
        plan.Features        = dto.Features;
        plan.IsActive        = dto.IsActive;
        plan.UpdatedAt       = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        return ResponseDto<SubscriptionPlanDto>.Success(MapToPlanDto(plan), "Cập nhật gói dịch vụ thành công.");
    }

    public async Task<ResponseDto<bool>> DeactivatePlanAsync(Guid id)
    {
        var plan = await _repo.GetPlanByIdAsync(id);
        if (plan == null)
            return ResponseDto<bool>.Fail("Không tìm thấy gói dịch vụ cần vô hiệu hóa.", 404);

        plan.IsActive  = false;
        plan.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();
        return ResponseDto<bool>.Success(true, "Vô hiệu hóa gói dịch vụ thành công.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SubscriptionPlanDto MapToPlanDto(SubscriptionPlan plan) => new()
    {
        Id              = plan.Id,
        Name            = plan.Name,
        Description     = plan.Description,
        Price           = plan.Price,
        BillingCycle    = plan.BillingCycle,
        AiRequestsLimit = plan.AiRequestsLimit,
        StorageLimitMb  = plan.StorageLimitMb,
        MaxPlans        = plan.MaxPlans,
        Features        = plan.Features,
        IsActive        = plan.IsActive
    };

    private static UserSubscriptionDto MapToUserSubscriptionDto(UserSubscription sub) => new()
    {
        Id                  = sub.Id,
        UserId              = sub.UserId,
        PlanId              = sub.PlanId,
        PlanName            = sub.Plan?.Name ?? "N/A",
        Status              = sub.Status,
        StartedAt           = sub.StartedAt,
        ExpiresAt           = sub.ExpiresAt,
        AiRequestsUsed      = sub.AiRequestsUsed,
        AiRequestsLimit     = sub.Plan?.AiRequestsLimit,
        RemainingAiRequests = sub.Plan?.AiRequestsLimit.HasValue == true
            ? Math.Max(0, sub.Plan.AiRequestsLimit.Value - sub.AiRequestsUsed)
            : null,
        StorageLimitMb      = sub.Plan?.StorageLimitMb,
        MaxPlans            = sub.Plan?.MaxPlans,
        CancelledAt         = sub.CancelledAt
    };
}
