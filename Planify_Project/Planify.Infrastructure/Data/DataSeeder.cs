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

        // Seed plan frameworks and templates
        if (!dbContext.PlanFrameworks.Any())
        {
            var adminUser = await userManager.FindByEmailAsync("admin123@gmail.com");
            var adminUserId = adminUser?.Id ?? Guid.Empty;

            var softwareFwId = Guid.NewGuid();
            var educationFwId = Guid.NewGuid();

            var frameworks = new List<PlanFramework>
            {
                new()
                {
                    Id = softwareFwId,
                    Name = "Lập trình & Phát triển Phần mềm",
                    Description = "Khung lập trình và phát triển phần mềm, ứng dụng, website.",
                    Keywords = "software,lập trình,phần mềm,web,app,coding,developer,it,code,git",
                    IsActive = true,
                    CreatedBy = adminUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = educationFwId,
                    Name = "Luyện thi Chứng chỉ & Học tập",
                    Description = "Khung kế hoạch học tập, ôn thi các chứng chỉ ngoại ngữ, chuyên môn.",
                    Keywords = "ielts,toeic,học tập,thi cử,chứng chỉ,study,english,tiếng anh,ôn thi",
                    IsActive = true,
                    CreatedBy = adminUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            dbContext.PlanFrameworks.AddRange(frameworks);
            await dbContext.SaveChangesAsync();

            var templates = new List<PlanTemplate>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FrameworkId = softwareFwId,
                    Title = "Quy trình phát triển Website 5 giai đoạn",
                    Description = "Cấu trúc chuẩn để xây dựng và phát triển một dự án website từ thiết kế đến vận hành.",
                    TemplateContent = "{\"tasks\":[{\"Title\":\"Giai đoạn 1: Thu thập yêu cầu và Lên kế hoạch\",\"subtasks\":[{\"Title\":\"Xác định mục tiêu và đối tượng mục tiêu của website\"},{\"Title\":\"Phác thảo Wireframe và Sơ đồ trang web (Sitemap)\"},{\"Title\":\"Lập bảng đặc tả kỹ thuật và chọn công nghệ sử dụng\"}]},{\"Title\":\"Giai đoạn 2: Thiết kế Giao diện (UI/UX Design)\",\"subtasks\":[{\"Title\":\"Thiết kế UI Mockup cho Trang chủ và các Trang con\"},{\"Title\":\"Tạo nguyên mẫu tương tác (Interactive Prototype)\"},{\"Title\":\"Nhận phản hồi từ khách hàng và phê duyệt thiết kế\"}]},{\"Title\":\"Giai đoạn 3: Phát triển Front-end & Back-end\",\"subtasks\":[{\"Title\":\"Lập trình giao diện Front-end responsive\"},{\"Title\":\"Thiết lập cơ sở dữ liệu và viết các API Back-end\"},{\"Title\":\"Tích hợp giao diện Front-end với hệ thống Back-end\"}]},{\"Title\":\"Giai đoạn 4: Kiểm thử và Sửa lỗi (QA/Testing)\",\"subtasks\":[{\"Title\":\"Kiểm thử chức năng và tính tương thích trên trình duyệt\"},{\"Title\":\"Kiểm tra hiệu năng và bảo mật hệ thống\"},{\"Title\":\"Sửa các lỗi phát hiện và tối ưu hóa tốc độ tải trang\"}]},{\"Title\":\"Giai đoạn 5: Triển khai và Bàn giao (Deployment)\",\"subtasks\":[{\"Title\":\"Cấu hình Server hosting/VPS và tên miền chính thức\"},{\"Title\":\"Deploy mã nguồn lên môi trường Production\"},{\"Title\":\"Hướng dẫn sử dụng và bàn giao tài liệu quản trị\"}]}]}",
                    IsActive = true,
                    CreatedBy = adminUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FrameworkId = educationFwId,
                    Title = "Lộ trình tự học IELTS 3 tháng bứt phá",
                    Description = "Lộ trình học tập tập trung vào cả 4 kỹ năng Nghe, Nói, Đọc, Viết phù hợp cho việc tự ôn luyện.",
                    TemplateContent = "{\"tasks\":[{\"Title\":\"Tháng 1: Củng cố nền tảng ngữ pháp & từ vựng và làm quen dạng đề\",\"subtasks\":[{\"Title\":\"Học từ vựng theo chủ đề phổ biến (Topic vocabulary)\"},{\"Title\":\"Ôn tập các chủ điểm ngữ pháp trọng điểm (Tenses, Passive Voice, Relative Clauses)\"},{\"Title\":\"Làm quen cấu trúc đề thi Listening và Reading\"}]},{\"Title\":\"Tháng 2: Luyện tập chuyên sâu từng kỹ năng & Phát triển chiến thuật làm bài\",\"subtasks\":[{\"Title\":\"Luyện kỹ năng Listening (Skimming & Scanning, Dictation)\"},{\"Title\":\"Luyện kỹ năng Reading (Matching Headings, True/False/Not Given)\"},{\"Title\":\"Luyện Writing Task 1 & Task 2 (Cách lên dàn ý, viết Introduction & Conclusion)\"},{\"Title\":\"Luyện Speaking Part 1, 2, 3 (Phát âm, độ trôi chảy và từ vựng nâng cao)\"}]},{\"Title\":\"Tháng 3: Luyện đề thi thử dưới áp lực thời gian & Khắc phục điểm yếu\",\"subtasks\":[{\"Title\":\"Làm đề thi thử trọn bộ (Full mock tests) 2 lần/tuần\"},{\"Title\":\"Phân tích chi tiết lỗi sai trong bài làm để rút kinh nghiệm\"},{\"Title\":\"Tổng duyệt chiến thuật quản lý thời gian cho phòng thi\"}]}]}",
                    IsActive = true,
                    CreatedBy = adminUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            dbContext.PlanTemplates.AddRange(templates);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded default frameworks and templates.");
        }
    }
}
