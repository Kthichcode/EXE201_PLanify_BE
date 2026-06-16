using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(15),
                    errorNumbersToAdd: null)));

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

        // ── Application Services (business logic thuần, không phụ thuộc infra) ─
        services.AddScoped<IAuthService,         AuthService>();
        services.AddScoped<IUserService,         UserService>();
        services.AddScoped<IPlanService,         PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionGuardService, SubscriptionGuardService>();
        services.AddScoped<IPlanFrameworkService, PlanFrameworkService>();
        services.AddScoped<IPlanTemplateService,  PlanTemplateService>();
        services.AddScoped<ICommunityPlanService, CommunityPlanService>();

        // ── OpenAI Chat Service (HttpClient + external API) ───────────────────
        services.AddHttpClient<IAiChatService, OpenAiChatService>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com");
            client.Timeout     = TimeSpan.FromSeconds(130);
        });

        // ── Email & Background Jobs ──────────────────────────────────────────
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        // Dùng IHttpClientFactory để gọi Resend HTTP API (thay SMTP bị block trên Render)
        services.AddHttpClient();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHostedService<DeadlineNotificationJob>();

        // ── SePay Payment Gateway ─────────────────────────────────────────────
        services.AddHttpClient<IPaymentService, PaymentService>(client =>
        {
            client.BaseAddress = new Uri("https://my.sepay.vn");
        });

        return services;
    }
}
