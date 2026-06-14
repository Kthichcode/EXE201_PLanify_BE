using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planify.Application.Interfaces;
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

    private async Task ProcessDeadlinesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var in24Hours = now.AddHours(24);

        // 1. Process Plans
        var upcomingPlans = await context.Plans
            .Where(p => p.Status != "done" 
                     && p.Deadline != null 
                     && p.Deadline > now 
                     && p.Deadline <= in24Hours 
                     && !p.IsReminderSent)
            .ToListAsync();

        foreach (var plan in upcomingPlans)
        {
            var user = await context.Users.FindAsync(plan.UserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                string subject = $"Nhắc nhở: Kế hoạch '{plan.Title}' sắp đến hạn!";
                string body = $@"
                    <h3>Chào {user.UserName},</h3>
                    <p>Kế hoạch <strong>{plan.Title}</strong> của bạn sẽ đến hạn vào lúc <strong>{plan.Deadline?.ToLocalTime():dd/MM/yyyy HH:mm}</strong>.</p>
                    <p>Hãy nhanh chóng hoàn thành nhé!</p>
                    <br>
                    <p>Trân trọng,<br>Planify Team</p>
                ";
                await emailService.SendEmailAsync(user.Email, subject, body);
                
                plan.IsReminderSent = true;
            }
        }

        // 2. Process PlanTasks
        var upcomingTasks = await context.PlanTasks
            .Include(t => t.Plan)
            .Where(t => t.Status != "done" 
                     && t.DueDate != null 
                     && t.DueDate > now 
                     && t.DueDate <= in24Hours 
                     && !t.IsReminderSent)
            .ToListAsync();

        foreach (var task in upcomingTasks)
        {
            if (task.Plan == null) continue;
            
            var user = await context.Users.FindAsync(task.Plan.UserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                string subject = $"Nhắc nhở: Công việc '{task.Title}' sắp đến hạn!";
                string body = $@"
                    <h3>Chào {user.UserName},</h3>
                    <p>Công việc <strong>{task.Title}</strong> (thuộc Kế hoạch {task.Plan.Title}) của bạn sẽ đến hạn vào lúc <strong>{task.DueDate?.ToLocalTime():dd/MM/yyyy HH:mm}</strong>.</p>
                    <p>Đừng quên hoàn thành nhé!</p>
                    <br>
                    <p>Trân trọng,<br>Planify Team</p>
                ";
                await emailService.SendEmailAsync(user.Email, subject, body);
                
                task.IsReminderSent = true;
            }
        }

        if (upcomingPlans.Any() || upcomingTasks.Any())
        {
            await context.SaveChangesAsync();
        }
    }
}
