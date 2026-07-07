using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Repositories;

public class PlanFeedbackRepository : IPlanFeedbackRepository
{
    private readonly ApplicationDbContext _context;

    public PlanFeedbackRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PlanFeedback feedback)
    {
        await _context.PlanFeedbacks.AddAsync(feedback);
    }

    public async Task<PlanFeedback?> GetByIdAsync(Guid id)
    {
        return await _context.PlanFeedbacks.FindAsync(id);
    }

    public async Task<List<PlanFeedback>> GetByPlanIdAsync(Guid planId)
    {
        return await _context.PlanFeedbacks
            .Where(f => f.PlanId == planId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<PlanFeedback> Items, int TotalCount)> GetAllPagedAsync(int page, int pageSize)
    {
        var query = _context.PlanFeedbacks
            .OrderByDescending(f => f.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsByUserAndPlanAsync(Guid userId, Guid planId)
    {
        return await _context.PlanFeedbacks
            .AnyAsync(f => f.UserId == userId && f.PlanId == planId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
