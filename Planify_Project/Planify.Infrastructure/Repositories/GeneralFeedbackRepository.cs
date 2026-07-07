using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Repositories;

public class GeneralFeedbackRepository : IGeneralFeedbackRepository
{
    private readonly ApplicationDbContext _context;

    public GeneralFeedbackRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GeneralFeedback feedback)
    {
        await _context.GeneralFeedbacks.AddAsync(feedback);
    }

    public async Task<GeneralFeedback?> GetByIdAsync(Guid id)
    {
        return await _context.GeneralFeedbacks.FindAsync(id);
    }

    public async Task<(List<GeneralFeedback> Items, int TotalCount)> GetAllPagedAsync(
        string? category, string? status, int page, int pageSize)
    {
        var query = _context.GeneralFeedbacks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(f => f.Category == category.ToLower());

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(f => f.Status == status.ToLower());

        query = query.OrderByDescending(f => f.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
