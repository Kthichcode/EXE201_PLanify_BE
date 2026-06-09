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

public class CommunityPlanRepository : ICommunityPlanRepository
{
    private readonly ApplicationDbContext _context;

    public CommunityPlanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CommunityPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.CommunityPlans.FirstOrDefaultAsync(cp => cp.Id == id, ct);

    public async Task<CommunityPlan?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.CommunityPlans
            .Include(cp => cp.Plan)
                .ThenInclude(p => p!.Tasks)
            .Include(cp => cp.Likes)
            .FirstOrDefaultAsync(cp => cp.Id == id, ct);

    public async Task<bool> ExistsByPlanIdAsync(Guid planId, CancellationToken ct = default)
        => await _context.CommunityPlans
            .AnyAsync(cp => cp.PlanId == planId && cp.Status != "removed", ct);

    public async Task<(List<CommunityPlan> Items, int TotalCount)> GetApprovedPagedAsync(
        string? search,
        Guid? categoryId,
        string? sortBy,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.CommunityPlans
            .Include(cp => cp.Plan)
            .Where(cp => cp.Status == "approved")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(cp =>
                cp.Title.Contains(search) ||
                (cp.Description != null && cp.Description.Contains(search)));

        if (categoryId.HasValue)
            query = query.Where(cp => cp.CategoryId == categoryId.Value);

        query = sortBy switch
        {
            "popular"          => query.OrderByDescending(cp => cp.LikeCount),
            "most_downloaded"  => query.OrderByDescending(cp => cp.DownloadCount),
            _                  => query.OrderByDescending(cp => cp.CreatedAt)  // newest
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(List<CommunityPlan> Items, int TotalCount)> GetPendingPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.CommunityPlans
            .Include(cp => cp.Plan)
            .Where(cp => cp.Status == "pending")
            .OrderBy(cp => cp.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<CommunityPlan>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.CommunityPlans
            .Include(cp => cp.Plan)
            .Where(cp => cp.UserId == userId)
            .OrderByDescending(cp => cp.CreatedAt)
            .ToListAsync(ct);

    public async Task<CommunityPlanLike?> GetLikeAsync(Guid communityPlanId, Guid userId, CancellationToken ct = default)
        => await _context.CommunityPlanLikes
            .FirstOrDefaultAsync(l => l.CommunityPlanId == communityPlanId && l.UserId == userId, ct);

    public async Task AddAsync(CommunityPlan communityPlan, CancellationToken ct = default)
        => await _context.CommunityPlans.AddAsync(communityPlan, ct);

    public async Task AddLikeAsync(CommunityPlanLike like, CancellationToken ct = default)
        => await _context.CommunityPlanLikes.AddAsync(like, ct);

    public async Task AddCopyAsync(PlanCopy copy, CancellationToken ct = default)
        => await _context.PlanCopies.AddAsync(copy, ct);

    public Task RemoveLikeAsync(CommunityPlanLike like, CancellationToken ct = default)
    {
        _context.CommunityPlanLikes.Remove(like);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await action();
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
