using Planify.Domain.Entities;

namespace Planify.Domain.Interfaces;

/// <summary>
/// Repository interface cho Plan entity.
/// Định nghĩa ở Domain vì đây là hợp đồng truy cập Domain Object.
/// Implementation: Infrastructure/Repositories/PlanRepository.cs
/// </summary>
public interface IPlanRepository
{
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Plan?> GetByIdWithTasksAsync(Guid id, CancellationToken ct = default);
    Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Plan plan, CancellationToken ct = default);
    Task DeleteAsync(Plan plan, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Thực thi action trong transaction — tự động commit/rollback.</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}
