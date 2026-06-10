using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Infrastructure.Identity;
using System;

namespace Planify.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanTask> PlanTasks => Set<PlanTask>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PlanFramework> PlanFrameworks => Set<PlanFramework>();
    public DbSet<PlanTemplate> PlanTemplates => Set<PlanTemplate>();
    public DbSet<CommunityPlan> CommunityPlans => Set<CommunityPlan>();
    public DbSet<CommunityPlanLike> CommunityPlanLikes => Set<CommunityPlanLike>();
    public DbSet<PlanCopy> PlanCopies => Set<PlanCopy>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.Entity<Plan>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Relate Plan to User
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(e => e.Template)
                .WithMany(t => t.Plans)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Framework)
                .WithMany(f => f.Plans)
                .HasForeignKey(e => e.FrameworkId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PlanTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Relate Task to Plan
            entity.HasOne(e => e.Plan)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Relate Task to ParentTask
            entity.HasOne(e => e.ParentTask)
                .WithMany(p => p.SubTasks)
                .HasForeignKey(e => e.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(20);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
        });

        builder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BillingCycle).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Price).HasPrecision(18, 2);
        });

        builder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Plan)
                .WithMany()
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
        });

        builder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths (User→PaymentTransaction and User→UserSubscription→PaymentTransaction)
            entity.HasOne(e => e.Subscription)
                .WithMany()
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });

        builder.Entity<PlanFramework>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Structure).IsRequired();
            entity.Property(e => e.Keywords).HasMaxLength(500);
            
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlanTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TemplateContent).IsRequired();
            
            entity.HasOne(e => e.Framework)
                .WithMany(f => f.Templates)
                .HasForeignKey(e => e.FrameworkId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CommunityPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PlanId).IsUnique();

            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(e => e.Plan)
                .WithMany()
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.ReviewedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<CommunityPlanLike>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Unique: mỗi user chỉ like 1 lần mỗi community plan
            entity.HasIndex(e => new { e.UserId, e.CommunityPlanId }).IsUnique();

            entity.HasOne(e => e.CommunityPlan)
                .WithMany(cp => cp.Likes)
                .HasForeignKey(e => e.CommunityPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PlanCopy>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.CommunityPlan)
                .WithMany(cp => cp.Copies)
                .HasForeignKey(e => e.CommunityPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.NewPlan)
                .WithMany()
                .HasForeignKey(e => e.NewPlanId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
