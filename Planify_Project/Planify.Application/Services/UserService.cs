using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.User;
using Planify.Application.DTOs.User.Response;
using Planify.Application.Interfaces;
using Planify.Domain.Interfaces;

namespace Planify.Application.Services;

/// <summary>
/// Business logic cho thông tin người dùng.
/// Không phụ thuộc vào bất kỳ thư viện hạ tầng nào.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly ISubscriptionRepository _subRepo;

    public UserService(IUserRepository userRepo, ISubscriptionRepository subRepo)
    {
        _userRepo = userRepo;
        _subRepo = subRepo;
    }

    public async Task<ResponseDto<UserProfileResponseDto>> GetProfileAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return ResponseDto<UserProfileResponseDto>.Fail("UserId không hợp lệ.", 400);

        var user = await _userRepo.FindByIdAsync(guid);
        if (user is null)
            return ResponseDto<UserProfileResponseDto>.Fail("Không tìm thấy người dùng.", 404);

        var profile = new UserProfileResponseDto
        {
            Id       = user.Id,
            Email    = user.Email,
            FullName = user.FullName
        };

        return ResponseDto<UserProfileResponseDto>.Success(profile, "Lấy thông tin thành công.", 200);
    }

    public async Task<ResponseDto<IEnumerable<UserAdminResponseDto>>> GetAllUsersForAdminAsync()
    {
        // 1. Lấy tất cả user
        var users = await _userRepo.GetAllUsersAsync();

        // 2. Lấy tất cả active subscriptions của các user này
        var userIds = users.Select(u => u.Id).ToList();
        var activeSubscriptions = await _subRepo.GetActiveSubscriptionsForUsersAsync(userIds);
        var subDict = activeSubscriptions.ToDictionary(s => s.UserId);

        var result = new List<UserAdminResponseDto>();

        // 3. Map dữ liệu
        foreach (var u in users)
        {
            var roles = await _userRepo.GetRolesAsync(u.Id);
            var sub = subDict.GetValueOrDefault(u.Id);

            result.Add(new UserAdminResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Roles = roles,
                PlanName = sub?.Plan?.Name,
                PlanStatus = sub?.Status,
                SubscriptionExpiresAt = sub?.ExpiresAt
            });
        }

        return ResponseDto<IEnumerable<UserAdminResponseDto>>.Success(result, "Lấy danh sách người dùng thành công.", 200);
    }

    public async Task<ResponseDto<OnboardingStatusDto>> GetOnboardingStatusAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return ResponseDto<OnboardingStatusDto>.Fail("UserId không hợp lệ.", 400);

        var user = await _userRepo.FindByIdAsync(guid);
        if (user is null)
            return ResponseDto<OnboardingStatusDto>.Fail("Không tìm thấy người dùng.", 404);

        return ResponseDto<OnboardingStatusDto>.Success(
            new OnboardingStatusDto { Status = user.OnboardingStatus, Step = user.OnboardingStep },
            "Lấy trạng thái onboarding thành công.", 200);
    }

    public async Task<ResponseDto<OnboardingStatusDto>> UpdateOnboardingAsync(string userId, UpdateOnboardingDto dto)
    {
        if (!Guid.TryParse(userId, out var guid))
            return ResponseDto<OnboardingStatusDto>.Fail("UserId không hợp lệ.", 400);

        var succeeded = await _userRepo.UpdateOnboardingAsync(guid, dto.Status, dto.Step);
        if (!succeeded)
            return ResponseDto<OnboardingStatusDto>.Fail("Không thể cập nhật trạng thái onboarding.", 500);

        return ResponseDto<OnboardingStatusDto>.Success(
            new OnboardingStatusDto { Status = dto.Status, Step = dto.Step },
            "Cập nhật onboarding thành công.", 200);
    }

    public async Task<ResponseDto<UserGrowthStatsDto>> GetUserGrowthStatsAsync(DateTime? from, DateTime? to)
    {
        // Mặc định: 30 ngày gần nhất (theo UTC)
        var utcNow = DateTime.UtcNow;
        var dateTo   = to?.Date   ?? utcNow.Date;
        var dateFrom = from?.Date ?? dateTo.AddDays(-29);

        if (dateFrom > dateTo)
            return ResponseDto<UserGrowthStatsDto>.Fail("Ngày bắt đầu phải trước ngày kết thúc.", 400);

        var raw = await _userRepo.GetUserGrowthAsync(
            dateFrom.ToUniversalTime(),
            dateTo.ToUniversalTime());

        // Tính growth rate so với khoảng trước
        double? growthRate = null;
        if (raw.PreviousRangeCount > 0)
            growthRate = Math.Round((raw.NewUsersInRange - raw.PreviousRangeCount) * 100.0 / raw.PreviousRangeCount, 2);

        // Điền đủ tất cả ngày trong khoảng (kể cả ngày không có đăng ký)
        var dailyDict = raw.DailyBreakdown.ToDictionary(x => x.Date, x => x.Count);
        var allDays   = new List<DailyRegistrationDto>();
        for (var d = dateFrom; d <= dateTo; d = d.AddDays(1))
        {
            var key = DateOnly.FromDateTime(d);
            allDays.Add(new DailyRegistrationDto
            {
                Date  = key,
                Count = dailyDict.GetValueOrDefault(key, 0)
            });
        }

        var result = new UserGrowthStatsDto
        {
            TotalUsers         = raw.TotalUsers,
            NewUsers           = raw.NewUsersInRange,
            NewUsersLast7Days  = raw.NewUsersLast7Days,
            NewUsersLast30Days = raw.NewUsersLast30Days,
            GrowthRatePercent  = growthRate,
            DailyRegistrations = allDays
        };

        return ResponseDto<UserGrowthStatsDto>.Success(result, "Lấy thống kê tăng trưởng thành công.", 200);
    }
}
