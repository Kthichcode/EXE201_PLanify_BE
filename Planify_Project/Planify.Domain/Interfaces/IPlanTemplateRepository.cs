using Planify.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Planify.Domain.Interfaces;

public interface IPlanTemplateRepository
{
    Task<PlanTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<PlanTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<List<PlanTemplate>> GetActiveAsync(CancellationToken ct = default);
    Task<List<PlanTemplate>> GetByFrameworkIdAsync(Guid frameworkId, CancellationToken ct = default);
    Task AddAsync(PlanTemplate template, CancellationToken ct = default);
    Task DeleteAsync(PlanTemplate template, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
