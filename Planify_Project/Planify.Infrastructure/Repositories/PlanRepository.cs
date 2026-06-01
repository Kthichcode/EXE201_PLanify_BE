using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;

namespace Planify.Infrastructure.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly ApplicationDbContext _context;

    public PlanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Plan?> GetByIdWithTasksAsync(Guid id, CancellationToken ct = default)
        => await _context.Plans
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Plans
            .Include(p => p.Tasks)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Plan plan, CancellationToken ct = default)
        => await _context.Plans.AddAsync(plan, ct);

    public Task DeleteAsync(Plan plan, CancellationToken ct = default)
    {
        _context.Plans.Remove(plan);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await action();
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
