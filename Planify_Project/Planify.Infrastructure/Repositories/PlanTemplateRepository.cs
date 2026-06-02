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

public class PlanTemplateRepository : IPlanTemplateRepository
{
    private readonly ApplicationDbContext _context;

    public PlanTemplateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlanTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PlanTemplates
            .Include(t => t.Framework)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<PlanTemplate>> GetAllAsync(CancellationToken ct = default)
        => await _context.PlanTemplates
            .Include(t => t.Framework)
            .OrderBy(t => t.Title)
            .ToListAsync(ct);

    public async Task<List<PlanTemplate>> GetActiveAsync(CancellationToken ct = default)
        => await _context.PlanTemplates
            .Include(t => t.Framework)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Title)
            .ToListAsync(ct);

    public async Task<List<PlanTemplate>> GetByFrameworkIdAsync(Guid frameworkId, CancellationToken ct = default)
        => await _context.PlanTemplates
            .Include(t => t.Framework)
            .Where(t => t.FrameworkId == frameworkId)
            .OrderBy(t => t.Title)
            .ToListAsync(ct);

    public async Task AddAsync(PlanTemplate template, CancellationToken ct = default)
        => await _context.PlanTemplates.AddAsync(template, ct);

    public Task DeleteAsync(PlanTemplate template, CancellationToken ct = default)
    {
        _context.PlanTemplates.Remove(template);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
