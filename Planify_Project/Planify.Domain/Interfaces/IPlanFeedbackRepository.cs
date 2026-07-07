using Planify.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.Domain.Interfaces;

public interface IPlanFeedbackRepository
{
    Task AddAsync(PlanFeedback feedback);
    Task<PlanFeedback?> GetByIdAsync(Guid id);
    Task<List<PlanFeedback>> GetByPlanIdAsync(Guid planId);
    Task<(List<PlanFeedback> Items, int TotalCount)> GetAllPagedAsync(int page, int pageSize);
    Task<bool> ExistsByUserAndPlanAsync(Guid userId, Guid planId);
    Task SaveChangesAsync();
}
