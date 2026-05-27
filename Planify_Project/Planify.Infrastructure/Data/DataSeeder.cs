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
                    Name = "Free",
                    Description = "Gói miễn phí cơ bản để làm quen với hệ thống",
                    Price = 0,
                    BillingCycle = "lifetime",
                    AiRequestsLimit = 10,
                    StorageLimitMb = 50,
                    MaxPlans = 3,
                    Features = "[\"AI Plan Generation (10 times/month)\", \"Basic Timeline Optimization\", \"3 Active Plans Limit\"]",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Premium",
                    Description = "Gói Premium cho cá nhân cần tối ưu công việc",
                    Price = 99000,
                    BillingCycle = "monthly",
                    AiRequestsLimit = 100,
                    StorageLimitMb = 500,
                    MaxPlans = 20,
                    Features = "[\"AI Plan Generation (100 times/month)\", \"Advanced Timeline Optimization\", \"20 Active Plans Limit\", \"Community Plan Sharing\"]",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Ultimate",
                    Description = "Gói không giới hạn dành cho người dùng chuyên nghiệp",
                    Price = 199000,
                    BillingCycle = "monthly",
                    AiRequestsLimit = null,
                    StorageLimitMb = null,
                    MaxPlans = null,
                    Features = "[\"Unlimited AI Plan Generation\", \"Unlimited Active Plans\", \"Priority AI Chat Support\", \"Priority Support & Backup\"]",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            dbContext.SubscriptionPlans.AddRange(plans);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded default subscription plans.");
        }
    }
}
