using Planify.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Planify.Domain.Interfaces;

public interface IPlanFrameworkRepository
{
    Task<PlanFramework?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<PlanFramework>> GetAllAsync(CancellationToken ct = default);
    Task<List<PlanFramework>> GetActiveAsync(CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken ct = default);
    /// <summary>
    /// Tìm framework active đầu tiên mà có keyword khớp với prompt của user (case-insensitive).
    /// </summary>
    Task<PlanFramework?> FindByKeywordAsync(string prompt, CancellationToken ct = default);
    Task AddAsync(PlanFramework framework, CancellationToken ct = default);
    Task DeleteAsync(PlanFramework framework, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
