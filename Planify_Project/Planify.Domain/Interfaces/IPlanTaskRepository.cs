using Planify.Domain.Entities;

namespace Planify.Domain.Interfaces;

/// <summary>
/// Repository interface cho PlanTask entity.
/// Implementation: Infrastructure/Repositories/PlanTaskRepository.cs
/// </summary>
public interface IPlanTaskRepository
{
    Task<PlanTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(PlanTask task, CancellationToken ct = default);
    Task<List<PlanTask>> GetByPlanIdAsync(Guid planId, CancellationToken ct = default);
    Task DeleteAsync(PlanTask task, CancellationToken ct = default);
    Task DeleteRangeAsync(IEnumerable<PlanTask> tasks, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
