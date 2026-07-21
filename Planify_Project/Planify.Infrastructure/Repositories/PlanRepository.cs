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

    public async Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Plans
            .CountAsync(p => p.UserId == userId
                          && p.Status != "draft"
                          && p.Status != "discarded", ct);

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
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
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
        });
    }

    public async Task<(int Total, int Completed, int Active)> GetStatsByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        // Lấy 1 lần, lọc ở memory — tránh nhiều round-trip DB
        var counts = await _context.Plans
            .Where(p => p.UserId == userId
                     && p.Status != "draft"
                     && p.Status != "discarded")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total     = g.Count(),
                Completed = g.Count(p => p.Status == "completed" || p.Progress >= 100)
            })
            .FirstOrDefaultAsync(ct);

        var total     = counts?.Total     ?? 0;
        var completed = counts?.Completed ?? 0;
        var active    = total - completed;

        return (total, completed, active);
    }
}
