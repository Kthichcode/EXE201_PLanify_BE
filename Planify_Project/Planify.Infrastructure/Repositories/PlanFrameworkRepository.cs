using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Repositories;

public class PlanFrameworkRepository : IPlanFrameworkRepository
{
    private readonly ApplicationDbContext _context;

    public PlanFrameworkRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlanFramework?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PlanFrameworks.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<List<PlanFramework>> GetAllAsync(CancellationToken ct = default)
        => await _context.PlanFrameworks.OrderBy(f => f.Name).ToListAsync(ct);

    public async Task<List<PlanFramework>> GetActiveAsync(CancellationToken ct = default)
        => await _context.PlanFrameworks.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync(ct);

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (excludeId.HasValue)
        {
            return await _context.PlanFrameworks.AnyAsync(f => f.Slug == slug && f.Id != excludeId.Value, ct);
        }
        return await _context.PlanFrameworks.AnyAsync(f => f.Slug == slug, ct);
    }

    public async Task AddAsync(PlanFramework framework, CancellationToken ct = default)
        => await _context.PlanFrameworks.AddAsync(framework, ct);

    public Task DeleteAsync(PlanFramework framework, CancellationToken ct = default)
    {
        _context.PlanFrameworks.Remove(framework);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
