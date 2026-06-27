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

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private static readonly TimeZoneInfo VietnamTz =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    private static string ToVietnamDate(DateTime? utcTime) =>
        utcTime.HasValue
            ? TimeZoneInfo.ConvertTimeFromUtc(utcTime.Value, VietnamTz).ToString("dd/MM/yyyy")
            : "N/A";

    /// <summary>
    /// Tạo HTML email tối giản, hiện đại — không emoji, không logo.
    /// </summary>
    private static string BuildEmailHtml(
        string userName,
        string accentColor,
        string badgeLabel,
        string mainTitle,
        string itemName,
        string? parentName,
        string deadlineTime,
        string actionMessage)
    {
        string parentRow = parentName is not null
            ? $@"<tr>
                    <td style=""padding:8px 0;color:#9ca3af;font-size:13px;font-weight:500;text-transform:uppercase;letter-spacing:0.5px;"">Thuộc kế hoạch</td>
                    <td style=""padding:8px 0;font-size:14px;font-weight:600;color:#374151;text-align:right;"">{parentName}</td>
                 </tr>"
            : "";

        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Planify</title>
</head>
<body style=""margin:0;padding:0;background-color:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f5f5f5;padding:48px 16px;"">
    <tr><td align=""center"">

      <table width=""560"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;"">

        <!-- Header -->
        <tr>
          <td style=""background:#4f46e5;padding:32px 40px;"">
            <div style=""font-size:22px;font-weight:700;color:#ffffff;letter-spacing:-0.5px;"">Planify</div>
            <div style=""font-size:12px;color:#f6f7f5;margin-top:4px;letter-spacing:0.5px;"">{badgeLabel}</div>
          </td>
        </tr>

        <!-- Accent line -->
        <tr>
          <td style=""height:3px;background:{accentColor};""></td>
        </tr>

        <!-- Body -->
        <tr>
          <td style=""padding:40px 40px 32px;"">

            <p style=""margin:0 0 8px;font-size:13px;font-weight:500;color:#71717a;text-transform:uppercase;letter-spacing:0.8px;"">Xin chào</p>
            <p style=""margin:0 0 32px;font-size:24px;font-weight:700;color:#18181b;line-height:1.3;"">{userName}</p>

            <p style=""margin:0 0 32px;font-size:15px;color:#52525b;line-height:1.75;"">
              {mainTitle}
            </p>

            <!-- Info block -->
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #e4e4e7;border-radius:8px;"">
              <tr>
                <td style=""padding:24px;"">
                  <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                    <tr>
                      <td style=""padding:8px 0;color:#71717a;font-size:13px;font-weight:500;text-transform:uppercase;letter-spacing:0.5px;"">Tên</td>
                      <td style=""padding:8px 0;font-size:14px;font-weight:600;color:#18181b;text-align:right;"">{itemName}</td>
                    </tr>
                    {parentRow}
                    <tr>
                      <td colspan=""2"" style=""padding:4px 0;""><div style=""border-top:1px solid #f4f4f5;""></div></td>
                    </tr>
                    <tr>
                      <td style=""padding:8px 0;color:#71717a;font-size:13px;font-weight:500;text-transform:uppercase;letter-spacing:0.5px;"">Hạn chót</td>
                      <td style=""padding:8px 0;font-size:16px;font-weight:700;color:{accentColor};text-align:right;"">{deadlineTime}</td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <p style=""margin:28px 0 0;font-size:14px;color:#71717a;line-height:1.7;"">
              {actionMessage}
            </p>

          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style=""padding:20px 40px;border-top:1px solid #f4f4f5;"">
            <p style=""margin:0;font-size:12px;color:#a1a1aa;"">
              Email này được gửi tự động bởi <strong style=""color:#18181b;"">Planify</strong>. Vui lòng không trả lời email này.
            </p>
          </td>
        </tr>

      </table>

    </td></tr>
  </table>
</body>
</html>";
    }

    private async Task ProcessDeadlinesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var nowUtc = DateTime.UtcNow;
        var todayVn    = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, VietnamTz).Date;
        var tomorrowVn = todayVn.AddDays(1);

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
