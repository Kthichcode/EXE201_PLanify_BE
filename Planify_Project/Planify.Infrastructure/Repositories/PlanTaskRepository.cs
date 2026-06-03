using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;

namespace Planify.Infrastructure.Repositories;

public class PlanTaskRepository : IPlanTaskRepository
{
    private readonly ApplicationDbContext _context;

    public PlanTaskRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlanTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PlanTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(PlanTask task, CancellationToken ct = default)
        => await _context.PlanTasks.AddAsync(task, ct);

    public async Task<List<PlanTask>> GetByPlanIdAsync(Guid planId, CancellationToken ct = default)
        => await _context.PlanTasks
            .Where(t => t.PlanId == planId)
            .ToListAsync(ct);

    public Task DeleteAsync(PlanTask task, CancellationToken ct = default)
    {
        _context.PlanTasks.Remove(task);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(IEnumerable<PlanTask> tasks, CancellationToken ct = default)
    {
        _context.PlanTasks.RemoveRange(tasks);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
