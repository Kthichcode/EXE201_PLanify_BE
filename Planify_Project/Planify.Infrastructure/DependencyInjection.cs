using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Planify.Application.Interfaces;
using Planify.Application.Services;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Identity;
using Planify.Infrastructure.Data;
using Planify.Infrastructure.Repositories;
using Planify.Infrastructure.Services;

namespace Planify.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext ─────────────────────────────────────────────────────────
        var rawCs = configuration.GetConnectionString("DefaultConnection")!;
        var effectiveCs = ResolveToIPv4ConnectionString(rawCs);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(effectiveCs));

        // ── ASP.NET Identity ──────────────────────────────────────────────────
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit            = true;
            options.Password.RequireLowercase        = true;
            options.Password.RequireUppercase        = false;
            options.Password.RequireNonAlphanumeric  = false;
            options.Password.RequiredLength          = 6;
            options.User.RequireUniqueEmail          = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // ── Infrastructure Services (phụ thuộc external/framework) ───────────
        services.AddScoped<ITokenService,        TokenService>();        // JWT (Infrastructure)
        services.AddScoped<IGoogleAuthValidator, GoogleAuthValidator>(); // Google SDK (Infrastructure)

        // ── Repositories (Infrastructure) ─────────────────────────────────────
        services.AddScoped<IUserRepository,         UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPlanRepository,         PlanRepository>();
        services.AddScoped<IPlanTaskRepository,     PlanTaskRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPlanFrameworkRepository, PlanFrameworkRepository>();
        services.AddScoped<IPlanTemplateRepository,  PlanTemplateRepository>();
        services.AddScoped<ICommunityPlanRepository, CommunityPlanRepository>();
        services.AddScoped<INotificationRepository,   NotificationRepository>();

        // ── Application Services (business logic thuần, không phụ thuộc infra) ─
        services.AddScoped<IAuthService,         AuthService>();
        services.AddScoped<IUserService,         UserService>();
        services.AddScoped<IPlanService,         PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionGuardService, SubscriptionGuardService>();
        services.AddScoped<IPlanFrameworkService, PlanFrameworkService>();
        services.AddScoped<IPlanTemplateService,  PlanTemplateService>();
        services.AddScoped<ICommunityPlanService, CommunityPlanService>();
        services.AddScoped<INotificationService,  NotificationService>();

        // ── OpenAI Chat Service (HttpClient + external API) ───────────────────
        services.AddHttpClient<IAiChatService, OpenAiChatService>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com");
            client.Timeout     = TimeSpan.FromSeconds(130);
        });

        // ── Email & Background Jobs ──────────────────────────────────────────
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddScoped<IEmailService, EmailService>();
        services.AddHostedService<DeadlineNotificationJob>();

        // ── SePay Payment Gateway ─────────────────────────────────────────────
        services.AddHttpClient<IPaymentService, PaymentService>(client =>
        {
            client.BaseAddress = new Uri("https://my.sepay.vn");
        });

        return services;
    }

    private static string ResolveToIPv4ConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = builder.Host;
            if (string.IsNullOrEmpty(host)) return connectionString;

            // Resolve hostname → pick first IPv4 address only
            var addresses = Dns.GetHostAddresses(host)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();

            if (addresses.Length > 0)
            {
                builder.Host = addresses[0].ToString();
                Console.WriteLine($"[IPv4] Resolved {host} → {builder.Host}");
            }
            else
            {
                Console.WriteLine($"[IPv4] No IPv4 address found for {host}, using original.");
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPv4] DNS resolve failed: {ex.Message}. Using original connection string.");
            return connectionString;
        }
    }
}
