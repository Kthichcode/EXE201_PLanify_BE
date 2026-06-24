using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planify.Application.Interfaces;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Services;

public class DeadlineNotificationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeadlineNotificationJob> _logger;

    public DeadlineNotificationJob(IServiceProvider serviceProvider, ILogger<DeadlineNotificationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("DeadlineNotificationJob running at: {time}", DateTimeOffset.Now);
            
            try
            {
                await ProcessDeadlinesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing DeadlineNotificationJob.");
            }

            // Run every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private static readonly TimeZoneInfo VietnamTz =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    private static string ToVietnamTime(DateTime? utcTime) =>
        utcTime.HasValue
            ? TimeZoneInfo.ConvertTimeFromUtc(utcTime.Value, VietnamTz).ToString("dd/MM/yyyy HH:mm")
            : "N/A";

    private async Task ProcessDeadlinesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var nowUtc = DateTime.UtcNow;
        // Tính ngày "ngày mai" theo giờ Việt Nam
        var todayVn   = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, VietnamTz).Date;
        var tomorrowVn = todayVn.AddDays(1);

        // Chuyển khoảng [bắt đầu ngày mai VN 00:00:00, kết thúc ngày mai VN 23:59:59] sang UTC để query DB
        // tomorrowVn là DateTime với time = 00:00:00 (từ .Date)
        var tomorrowStartUtc = TimeZoneInfo.ConvertTimeToUtc(tomorrowVn, VietnamTz);
        var tomorrowEndUtc   = TimeZoneInfo.ConvertTimeToUtc(tomorrowVn.AddDays(1).AddTicks(-1), VietnamTz);

        // 1. Process Plans
        var upcomingPlans = await context.Plans
            .Where(p => p.Status != "done"
                     && p.Deadline != null
                     && p.Deadline >= tomorrowStartUtc
                     && p.Deadline <= tomorrowEndUtc
                     && !p.IsReminderSent)
            .ToListAsync();

        foreach (var plan in upcomingPlans)
        {
            var user = await context.Users.FindAsync(plan.UserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                string title = $"Nhắc nhở: Kế hoạch '{plan.Title}' sắp đến hạn!";
                string body = $@"
                    <h3>Chào {user.UserName},</h3>
                    <p>Kế hoạch <strong>{plan.Title}</strong> của bạn sẽ đến hạn vào ngày <strong>{ToVietnamTime(plan.Deadline)}</strong> (ngày mai).</p>
                    <p>Hãy nhanh chóng hoàn thành nhé!</p>
                    <br>
                    <p>Trân trọng,<br>Planify Team</p>
                ";
                await emailService.SendEmailAsync(user.Email, title, body);

                plan.IsReminderSent = true;

                // Add In-App Notification
                await notificationRepo.AddAsync(new Domain.Entities.Notification
                {
                    UserId = user.Id,
                    Title = "Sắp đến hạn kế hoạch",
                    Message = $"Kế hoạch '{plan.Title}' sẽ đến hạn vào {plan.Deadline?.ToLocalTime():dd/MM/yyyy HH:mm}.",
                    Type = "deadline",
                    ReferenceId = plan.Id
                });
            }
        }

        // 2. Process PlanTasks
        var upcomingTasks = await context.PlanTasks
            .Include(t => t.Plan)
            .Where(t => t.Status != "done"
                     && t.DueDate != null
                     && t.DueDate >= tomorrowStartUtc
                     && t.DueDate <= tomorrowEndUtc
                     && !t.IsReminderSent)
            .ToListAsync();

        foreach (var task in upcomingTasks)
        {
            if (task.Plan == null) continue;
            
            var user = await context.Users.FindAsync(task.Plan.UserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                string title = $"Nhắc nhở: Công việc '{task.Title}' sắp đến hạn!";
                string body = $@"
                    <h3>Chào {user.UserName},</h3>
                    <p>Công việc <strong>{task.Title}</strong> (thuộc Kế hoạch {task.Plan.Title}) của bạn sẽ đến hạn vào lúc <strong>{ToVietnamTime(task.DueDate)}</strong>.</p>
                    <p>Đừng quên hoàn thành nhé!</p>
                    <br>
                    <p>Trân trọng,<br>Planify Team</p>
                ";
                await emailService.SendEmailAsync(user.Email, title, body);
                
                task.IsReminderSent = true;

                // Add In-App Notification
                await notificationRepo.AddAsync(new Domain.Entities.Notification
                {
                    UserId = user.Id,
                    Title = "Sắp đến hạn công việc",
                    Message = $"Công việc '{task.Title}' sẽ đến hạn vào {task.DueDate?.ToLocalTime():dd/MM/yyyy HH:mm}.",
                    Type = "deadline",
                    ReferenceId = task.Id
                });
            }
        }

        if (upcomingPlans.Any() || upcomingTasks.Any())
        {
            await context.SaveChangesAsync();
            await notificationRepo.SaveChangesAsync();
        }
    }
}
