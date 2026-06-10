using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planify.Domain.Entities;
using Planify.Infrastructure.Identity;
using System.Collections.Generic;

namespace Planify.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Seed roles
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        // Seed admin user
        const string adminEmail = "admin123@gmail.com";
        const string adminPassword = "Abc123@";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Seeded admin user: {Email}", adminEmail);
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to seed admin user: {Errors}", errors);
            }
        }
        else
        {
            logger.LogInformation("Admin user already exists, skipping seed.");
        }

        // Seed subscription plans
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!dbContext.SubscriptionPlans.Any())
        {
            var plans = new List<SubscriptionPlan>
            {
                new()
                {
                    Name            = "Free",
                    Description     = "Gói miễn phí cơ bản để làm quen với hệ thống",
                    Price           = 0,
                    BillingCycle    = "lifetime",
                    Tier            = "free",
                    AiRequestsLimit = 3,
                    AiRefineLimit   = 3,
                    StorageLimitMb  = 50,
                    MaxPlans        = 3,
                    Features        = "[\"3 lượt tạo kế hoạch AI\", \"3 lượt chỉnh sửa AI\", \"Lưu tối đa 3 kế hoạch\", \"Xem thư viện cộng đồng\"]",
                    IsActive        = true,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow
                },
                new()
                {
                    Name            = "Premium",
                    Description     = "Gói Premium cho cá nhân cần tối ưu công việc",
                    Price           = 69000,
                    BillingCycle    = "monthly",
                    Tier            = "premium",
                    AiRequestsLimit = 20,
                    AiRefineLimit   = 20,
                    StorageLimitMb  = 500,
                    MaxPlans        = 10,
                    Features        = "[\"20 lượt tạo kế hoạch AI\", \"20 lượt chỉnh sửa AI (+1/ngày khi hết)\", \"Lưu tối đa 10 kế hoạch\", \"Chia sẻ & lưu kế hoạch từ thư viện\"]",
                    IsActive        = true,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow
                },
                new()
                {
                    Name            = "VIP",
                    Description     = "Gói không giới hạn dành cho người dùng chuyên nghiệp",
                    Price           = 109000,
                    BillingCycle    = "monthly",
                    Tier            = "vip",
                    AiRequestsLimit = null,   // không giới hạn
                    AiRefineLimit   = null,   // không giới hạn
                    StorageLimitMb  = null,
                    MaxPlans        = null,   // không giới hạn
                    Features        = "[\"Không giới hạn lượt tạo kế hoạch AI\", \"Không giới hạn lượt chỉnh sửa AI\", \"Lưu trữ không giới hạn\", \"Chia sẻ & lưu kế hoạch từ thư viện\", \"Đầy đủ tính năng Premium\"]",
                    IsActive        = true,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow
                }
            };

            dbContext.SubscriptionPlans.AddRange(plans);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded default subscription plans.");
        }
    }
}
