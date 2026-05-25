using Planify.Application.DTOs.Plans;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Planify.Infrastructure.Services;

public class PlanService : IPlanService
{
    private readonly ApplicationDbContext _context;

    public PlanService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlanDto> CreateManualPlanAsync(CreatePlanDto dto, Guid userId)
    {
        var plan = new Plan
        {
            UserId = userId,
            TemplateId = dto.TemplateId,
            FrameworkId = dto.FrameworkId,
            CategoryId = dto.CategoryId,
            Title = dto.Title,
            Description = dto.Description,
            Goal = dto.Goal,
            IsPublic = dto.IsPublic,
            Deadline = dto.Deadline,
            Status = "active",
            Progress = 0,
            IsAIGenerated = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        return MapToDto(plan);
    }

    public async Task<PlanTaskDto> AddTaskToPlanAsync(Guid planId, CreatePlanTaskDto dto, Guid userId)
    {
        var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId);
        if (plan == null)
        {
            throw new Exception("Plan not found or you do not have permission to access it.");
        }

        var planTask = new PlanTask
        {
            PlanId = plan.Id,
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority ?? "medium",
            StartDate = dto.StartDate,
            DueDate = dto.DueDate,
            OrderIndex = dto.OrderIndex,
            Status = "todo",
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PlanTasks.Add(planTask);
        await _context.SaveChangesAsync();

        return new PlanTaskDto
        {
            Id = planTask.Id,
            PlanId = planTask.PlanId,
            ParentTaskId = planTask.ParentTaskId,
            Title = planTask.Title,
            Description = planTask.Description,
            Status = planTask.Status,
            Priority = planTask.Priority,
            StartDate = planTask.StartDate,
            DueDate = planTask.DueDate,
            CompletedAt = planTask.CompletedAt,
            Progress = planTask.Progress,
            OrderIndex = planTask.OrderIndex,
            CreatedAt = planTask.CreatedAt,
            UpdatedAt = planTask.UpdatedAt
        };
    }

    public async Task<PlanDto?> GetPlanByIdAsync(Guid planId, Guid userId)
    {
        var plan = await _context.Plans
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId);

        if (plan == null)
        {
            return null;
        }

        return MapToDto(plan);
    }

    private PlanDto MapToDto(Plan plan)
    {
        return new PlanDto
        {
            Id = plan.Id,
            UserId = plan.UserId,
            TemplateId = plan.TemplateId,
            FrameworkId = plan.FrameworkId,
            CategoryId = plan.CategoryId,
            Title = plan.Title,
            Description = plan.Description,
            Goal = plan.Goal,
            Status = plan.Status,
            IsPublic = plan.IsPublic,
            Deadline = plan.Deadline,
            Progress = plan.Progress,
            IsAIGenerated = plan.IsAIGenerated,
            SortOrder = plan.SortOrder,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            Tasks = plan.Tasks?.Select(t => new PlanTaskDto
            {
                Id = t.Id,
                PlanId = t.PlanId,
                ParentTaskId = t.ParentTaskId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                StartDate = t.StartDate,
                DueDate = t.DueDate,
                CompletedAt = t.CompletedAt,
                Progress = t.Progress,
                OrderIndex = t.OrderIndex,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList() ?? new System.Collections.Generic.List<PlanTaskDto>()
        };
    }
}
