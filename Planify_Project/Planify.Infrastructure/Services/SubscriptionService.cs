using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Subscriptions;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Infrastructure.Data;

namespace Planify.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;

    public SubscriptionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ResponseDto<IEnumerable<SubscriptionPlanDto>>> GetActivePlansAsync()
    {
        var plans = await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Select(p => MapToPlanDto(p))
            .ToListAsync();

        return ResponseDto<IEnumerable<SubscriptionPlanDto>>.Success(plans, "Lấy danh sách gói dịch vụ thành công.");
    }

    public async Task<ResponseDto<UserSubscriptionDto>> GetUserSubscriptionAsync(Guid userId)
    {
        var currentSub = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "active");

        if (currentSub == null)
        {
            // Try to auto-assign the Free plan
            var freePlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Name.ToLower() == "free" && p.IsActive);

            if (freePlan != null)
            {
                currentSub = new UserSubscription
                {
                    UserId = userId,
                    PlanId = freePlan.Id,
                    Status = "active",
                    StartedAt = DateTime.UtcNow,
                    ExpiresAt = null,
                    AiRequestsUsed = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Plan = freePlan
                };

                _context.UserSubscriptions.Add(currentSub);
                await _context.SaveChangesAsync();
            }
            else
            {
                return ResponseDto<UserSubscriptionDto>.Fail("Không tìm thấy gói hiện tại của người dùng và không có gói Free mặc định nào được cấu hình.", 404);
            }
        }
        else if (currentSub.ExpiresAt.HasValue && currentSub.ExpiresAt.Value < DateTime.UtcNow)
        {
            // Expire subscription
            currentSub.Status = "expired";
            currentSub.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Try to assign Free plan
            return await GetUserSubscriptionAsync(userId);
        }

        return ResponseDto<UserSubscriptionDto>.Success(MapToUserSubscriptionDto(currentSub), "Lấy thông tin gói sử dụng thành công.");
    }

    public async Task<ResponseDto<UserSubscriptionDto>> UpgradeSubscriptionAsync(Guid userId, UpgradeSubscriptionRequestDto dto)
    {
        var plan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == dto.PlanId && p.IsActive);

        if (plan == null)
        {
            return ResponseDto<UserSubscriptionDto>.Fail("Gói dịch vụ không tồn tại hoặc đã bị ngừng hoạt động.", 404);
        }

        // Deactivate existing active subscriptions
        var activeSubs = await _context.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "active")
            .ToListAsync();

        foreach (var sub in activeSubs)
        {
            sub.Status = "cancelled";
            sub.CancelledAt = DateTime.UtcNow;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        // Calculate expiration date
        DateTime? expiresAt = null;
        if (plan.BillingCycle.ToLower() == "monthly")
        {
            expiresAt = DateTime.UtcNow.AddMonths(1);
        }
        else if (plan.BillingCycle.ToLower() == "yearly")
        {
            expiresAt = DateTime.UtcNow.AddYears(1);
        }

        // Create new UserSubscription
        var newSub = new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = "active",
            StartedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            AiRequestsUsed = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Plan = plan
        };

        _context.UserSubscriptions.Add(newSub);
        await _context.SaveChangesAsync(); // save to generate newSub.Id

        // Create payment transaction simulation
        var txn = new PaymentTransaction
        {
            UserId = userId,
            SubscriptionId = newSub.Id,
            Amount = plan.Price,
            Currency = "VND",
            Status = "success",
            PaymentMethod = dto.PaymentMethod,
            PaymentRef = "TXN_" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentTransactions.Add(txn);
        await _context.SaveChangesAsync();

        return ResponseDto<UserSubscriptionDto>.Success(MapToUserSubscriptionDto(newSub), "Nâng cấp gói dịch vụ thành công.");
    }

    public async Task<ResponseDto<IEnumerable<SubscriptionPlanDto>>> GetAllPlansAsync()
    {
        var plans = await _context.SubscriptionPlans
            .OrderBy(p => p.Price)
            .Select(p => MapToPlanDto(p))
            .ToListAsync();

        return ResponseDto<IEnumerable<SubscriptionPlanDto>>.Success(plans, "Lấy toàn bộ danh sách gói thành công.");
    }

    public async Task<ResponseDto<SubscriptionPlanDto>> CreatePlanAsync(CreateSubscriptionPlanDto dto)
    {
        var exists = await _context.SubscriptionPlans.AnyAsync(p => p.Name.ToLower() == dto.Name.ToLower());
        if (exists)
        {
            return ResponseDto<SubscriptionPlanDto>.Fail("Tên gói dịch vụ đã tồn tại.", 400);
        }

        var plan = new SubscriptionPlan
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            BillingCycle = dto.BillingCycle,
            AiRequestsLimit = dto.AiRequestsLimit,
            StorageLimitMb = dto.StorageLimitMb,
            MaxPlans = dto.MaxPlans,
            Features = dto.Features,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync();

        return ResponseDto<SubscriptionPlanDto>.Success(MapToPlanDto(plan), "Tạo gói dịch vụ mới thành công.", 201);
    }

    public async Task<ResponseDto<SubscriptionPlanDto>> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanDto dto)
    {
        var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null)
        {
            return ResponseDto<SubscriptionPlanDto>.Fail("Không tìm thấy gói dịch vụ cần chỉnh sửa.", 404);
        }

        var nameExists = await _context.SubscriptionPlans
            .AnyAsync(p => p.Id != id && p.Name.ToLower() == dto.Name.ToLower());

        if (nameExists)
        {
            return ResponseDto<SubscriptionPlanDto>.Fail("Tên gói dịch vụ đã được sử dụng ở gói khác.", 400);
        }

        plan.Name = dto.Name;
        plan.Description = dto.Description;
        plan.Price = dto.Price;
        plan.BillingCycle = dto.BillingCycle;
        plan.AiRequestsLimit = dto.AiRequestsLimit;
        plan.StorageLimitMb = dto.StorageLimitMb;
        plan.MaxPlans = dto.MaxPlans;
        plan.Features = dto.Features;
        plan.IsActive = dto.IsActive;
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ResponseDto<SubscriptionPlanDto>.Success(MapToPlanDto(plan), "Cập nhật gói dịch vụ thành công.");
    }

    public async Task<ResponseDto<bool>> DeactivatePlanAsync(Guid id)
    {
        var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null)
        {
            return ResponseDto<bool>.Fail("Không tìm thấy gói dịch vụ cần vô hiệu hóa.", 404);
        }

        plan.IsActive = false;
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ResponseDto<bool>.Success(true, "Vô hiệu hóa gói dịch vụ thành công.");
    }

    // Helpers
    private static SubscriptionPlanDto MapToPlanDto(SubscriptionPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Description = plan.Description,
        Price = plan.Price,
        BillingCycle = plan.BillingCycle,
        AiRequestsLimit = plan.AiRequestsLimit,
        StorageLimitMb = plan.StorageLimitMb,
        MaxPlans = plan.MaxPlans,
        Features = plan.Features,
        IsActive = plan.IsActive
    };

    private static UserSubscriptionDto MapToUserSubscriptionDto(UserSubscription sub) => new()
    {
        Id = sub.Id,
        UserId = sub.UserId,
        PlanId = sub.PlanId,
        PlanName = sub.Plan?.Name ?? "N/A",
        Status = sub.Status,
        StartedAt = sub.StartedAt,
        ExpiresAt = sub.ExpiresAt,
        AiRequestsUsed = sub.AiRequestsUsed,
        AiRequestsLimit = sub.Plan?.AiRequestsLimit,
        RemainingAiRequests = sub.Plan?.AiRequestsLimit.HasValue == true
            ? Math.Max(0, sub.Plan.AiRequestsLimit.Value - sub.AiRequestsUsed)
            : null,
        StorageLimitMb = sub.Plan?.StorageLimitMb,
        MaxPlans = sub.Plan?.MaxPlans,
        CancelledAt = sub.CancelledAt
    };
}
