using Planify.Domain.Entities;

namespace Planify.Domain.Interfaces;

/// <summary>
/// Repository interface cho việc quản lý tài khoản người dùng.
/// Wrap toàn bộ UserManager (ASP.NET Identity) của Infrastructure.
/// Implementation: Infrastructure/Repositories/UserRepository.cs
/// </summary>
public interface IUserRepository
{
    Task<UserAccountDto?> FindByEmailAsync(string email);
    Task<UserAccountDto?> FindByIdAsync(Guid id);
    Task<List<UserAccountDto>> GetAllUsersAsync();

    /// <summary>Tạo user với password (Register thường).</summary>
    Task<(bool Succeeded, IEnumerable<string> Errors)> CreateWithPasswordAsync(
        string email, string fullName, string password);

    /// <summary>Tạo user không có password (đăng nhập Google/external).</summary>
    Task<(bool Succeeded, IEnumerable<string> Errors)> CreateExternalAsync(
        string email, string fullName);

    Task<bool> CheckPasswordAsync(Guid userId, string password);
    Task<IList<string>> GetRolesAsync(Guid userId);

    /// <summary>Cập nhật trạng thái và bước onboarding của user.</summary>
    Task<bool> UpdateOnboardingAsync(Guid userId, string status, int step);

    /// <summary>
    /// Trả về thống kê tăng trưởng user: tổng số, số mới theo khoảng thời gian,
    /// và số đăng ký theo từng ngày trong khoảng [from, to].
    /// </summary>
    Task<UserGrowthRawDto> GetUserGrowthAsync(DateTime from, DateTime to);
}

/// <summary>
/// DTO nội bộ đại diện cho một tài khoản người dùng.
/// Dùng để truyền dữ liệu giữa các tầng mà không expose ApplicationUser (ASP.NET Identity).
/// </summary>
public record UserAccountDto(
    Guid   Id,
    string Email,
    string FullName,
    bool   EmailConfirmed,
    string OnboardingStatus  = "not_started",
    int    OnboardingStep    = 0,
    DateTime CreatedAt       = default
);

/// <summary>Raw data thống kê tăng trưởng user từ repository.</summary>
public record UserGrowthRawDto(
    int TotalUsers,
    int NewUsersInRange,
    int NewUsersLast7Days,
    int NewUsersLast30Days,
    int PreviousRangeCount,
    List<(DateOnly Date, int Count)> DailyBreakdown
);
