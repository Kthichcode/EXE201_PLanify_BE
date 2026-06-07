using Planify.Domain.Entities;

namespace Planify.Domain.Interfaces;

/// <summary>
/// Repository interface cho SubscriptionPlan, UserSubscription, PaymentTransaction entities.
/// Implementation: Infrastructure/Repositories/SubscriptionRepository.cs
/// </summary>
public interface ISubscriptionRepository
{
    // SubscriptionPlan
    Task<List<SubscriptionPlan>> GetActivePlansAsync(CancellationToken ct = default);
    Task<List<SubscriptionPlan>> GetAllPlansAsync(CancellationToken ct = default);
    Task<SubscriptionPlan?> GetPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetPlanByNameAsync(string name, CancellationToken ct = default);
    Task<bool> PlanNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task AddPlanAsync(SubscriptionPlan plan, CancellationToken ct = default);

    // UserSubscription
    Task<UserSubscription?> GetActiveUserSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<List<UserSubscription>> GetActiveUserSubscriptionsAsync(Guid userId, CancellationToken ct = default);
    Task AddUserSubscriptionAsync(UserSubscription subscription, CancellationToken ct = default);

    // PaymentTransaction
    Task AddPaymentTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default);
    Task<PaymentTransaction?> GetPaymentTransactionByRefAsync(string paymentRef, CancellationToken ct = default);
    Task<List<PaymentTransaction>> GetSuccessfulTransactionsAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
