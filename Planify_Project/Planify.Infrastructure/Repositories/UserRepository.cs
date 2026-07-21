using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Identity;

namespace Planify.Infrastructure.Repositories;

/// <summary>
/// Implements IUserRepository bằng UserManager&lt;ApplicationUser&gt; của ASP.NET Identity.
/// Map ApplicationUser → UserAccountDto để Application layer không cần biết đến ApplicationUser.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserAccountDto?> FindByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : ToDto(user);
    }

    public async Task<List<UserAccountDto>> GetAllUsersAsync()
    {
        var users = await Task.FromResult(_userManager.Users.ToList());
        return users.Select(ToDto).ToList();
    }

    public async Task<UserAccountDto?> FindByIdAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        return user is null ? null : ToDto(user);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> CreateWithPasswordAsync(
        string email, string fullName, string password)
    {
        var user = new ApplicationUser
        {
            Email     = email,
            UserName  = email,
            FullName  = fullName
        };

        var result = await _userManager.CreateAsync(user, password);
        return (result.Succeeded, result.Errors.Select(e => e.Description));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> CreateExternalAsync(
        string email, string fullName)
    {
        var user = new ApplicationUser
        {
            Email          = email,
            UserName       = email,
            FullName       = fullName,
            EmailConfirmed = true   // Google đã xác minh email
        };

        var result = await _userManager.CreateAsync(user);
        return (result.Succeeded, result.Errors.Select(e => e.Description));
    }

    public async Task<bool> CheckPasswordAsync(Guid userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<IList<string>> GetRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return new List<string>();
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<bool> UpdateOnboardingAsync(Guid userId, string status, int step)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        user.OnboardingStatus = status;
        user.OnboardingStep   = step;

        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<UserGrowthRawDto> GetUserGrowthAsync(DateTime from, DateTime to)
    {
        var toEndOfDay = to.Date.AddDays(1).AddTicks(-1); // inclusive end
        var now        = DateTime.UtcNow;
        var sevenDaysAgo  = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        // Khoảng trước tương đương để tính growth rate
        var rangeDays = (to.Date - from.Date).TotalDays + 1;
        var prevFrom  = from.AddDays(-rangeDays);
        var prevTo    = from.AddTicks(-1);

        var allUsers = _userManager.Users.AsQueryable();

        var totalUsers        = await allUsers.CountAsync();
        var newInRange        = await allUsers.CountAsync(u => u.CreatedAt >= from && u.CreatedAt <= toEndOfDay);
        var newLast7          = await allUsers.CountAsync(u => u.CreatedAt >= sevenDaysAgo);
        var newLast30         = await allUsers.CountAsync(u => u.CreatedAt >= thirtyDaysAgo);
        var previousCount     = await allUsers.CountAsync(u => u.CreatedAt >= prevFrom && u.CreatedAt <= prevTo);

        // Group by ngày (dùng EF, tránh client-side grouping)
        var rawDaily = await allUsers
            .Where(u => u.CreatedAt >= from && u.CreatedAt <= toEndOfDay)
            .Select(u => new { u.CreatedAt })
            .ToListAsync();

        var dailyBreakdown = rawDaily
            .GroupBy(u => DateOnly.FromDateTime(u.CreatedAt))
            .Select(g => (Date: g.Key, Count: g.Count()))
            .OrderBy(x => x.Date)
            .ToList();

        return new UserGrowthRawDto(
            TotalUsers:        totalUsers,
            NewUsersInRange:   newInRange,
            NewUsersLast7Days: newLast7,
            NewUsersLast30Days:newLast30,
            PreviousRangeCount:previousCount,
            DailyBreakdown:    dailyBreakdown
        );
    }

    // ── Mapper ───────────────────────────────────────────────────────────────

    private static UserAccountDto ToDto(ApplicationUser user) =>
        new(user.Id, user.Email!, user.FullName, user.EmailConfirmed,
            user.OnboardingStatus, user.OnboardingStep, user.CreatedAt);
}

