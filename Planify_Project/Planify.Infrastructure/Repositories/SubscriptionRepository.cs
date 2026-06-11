using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;

namespace Planify.Infrastructure.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public SubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SubscriptionPlan>> GetActivePlansAsync(CancellationToken ct = default)
        => await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync(ct);

    public async Task<List<SubscriptionPlan>> GetAllPlansAsync(CancellationToken ct = default)
        => await _context.SubscriptionPlans
            .OrderBy(p => p.Price)
            .ToListAsync(ct);

    public async Task<SubscriptionPlan?> GetPlanByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<SubscriptionPlan?> GetPlanByNameAsync(string name, CancellationToken ct = default)
        => await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower(), ct);

    public async Task<bool> PlanNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        => await _context.SubscriptionPlans
            .AnyAsync(p => p.Name.ToLower() == name.ToLower()
                           && (!excludeId.HasValue || p.Id != excludeId.Value), ct);

    public async Task AddPlanAsync(SubscriptionPlan plan, CancellationToken ct = default)
        => await _context.SubscriptionPlans.AddAsync(plan, ct);

    public async Task<UserSubscription?> GetActiveUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status == "active")
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<List<UserSubscription>> GetActiveUserSubscriptionsAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "active")
            .ToListAsync(ct);

    public async Task AddUserSubscriptionAsync(UserSubscription subscription, CancellationToken ct = default)
        => await _context.UserSubscriptions.AddAsync(subscription, ct);

    public Task UpdateUserSubscriptionAsync(UserSubscription subscription, CancellationToken ct = default)
    {
        // EF Core theo dõi entity đã tracked → chỉ cần SaveChanges
        _context.UserSubscriptions.Update(subscription);
        return Task.CompletedTask;
    }

    public async Task AddPaymentTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default)
        => await _context.PaymentTransactions.AddAsync(transaction, ct);

    public async Task<PaymentTransaction?> GetPaymentTransactionByRefAsync(string paymentRef, CancellationToken ct = default)
        => await _context.PaymentTransactions
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.PaymentRef == paymentRef, ct);

    public async Task<List<PaymentTransaction>> GetSuccessfulTransactionsAsync(CancellationToken ct = default)
        => await _context.PaymentTransactions
            .Where(t => t.Status == "success")
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
