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
    Task<PlanFramework?> FindByKeywordAsync(string prompt, CancellationToken ct = default);
    Task AddAsync(PlanFramework framework, CancellationToken ct = default);
    Task DeleteAsync(PlanFramework framework, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

