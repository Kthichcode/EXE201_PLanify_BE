using Microsoft.AspNetCore.Identity;
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

    // ── Mapper ───────────────────────────────────────────────────────────────

    private static UserAccountDto ToDto(ApplicationUser user) =>
        new(user.Id, user.Email!, user.FullName, user.EmailConfirmed,
            user.OnboardingStatus, user.OnboardingStep);
}
