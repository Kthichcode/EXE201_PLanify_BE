using Planify.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.Domain.Interfaces;

public interface IGeneralFeedbackRepository
{
    Task AddAsync(GeneralFeedback feedback);
    Task<GeneralFeedback?> GetByIdAsync(Guid id);

    Task<(List<GeneralFeedback> Items, int TotalCount)> GetAllPagedAsync(
        string? category, string? status, int page, int pageSize);

    Task SaveChangesAsync();
}
