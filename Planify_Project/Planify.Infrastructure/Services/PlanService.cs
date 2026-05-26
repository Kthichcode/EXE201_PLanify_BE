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

        if (dto.ParentTaskId.HasValue)
        {
            var parentExists = await _context.PlanTasks.AnyAsync(t => t.Id == dto.ParentTaskId.Value && t.PlanId == plan.Id);
            if (!parentExists)
            {
                throw new Exception("Parent task not found in this plan.");
            }
        }

        var planTask = new PlanTask
        {
            PlanId = plan.Id,
            ParentTaskId = dto.ParentTaskId,
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
            Tasks = BuildTaskTree(plan.Tasks)
        };
    }

    private System.Collections.Generic.List<PlanTaskDto> BuildTaskTree(System.Collections.Generic.ICollection<PlanTask>? tasks)
    {
        if (tasks == null || !tasks.Any()) return new System.Collections.Generic.List<PlanTaskDto>();

        var allTaskDtos = tasks.Select(t => new PlanTaskDto
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
        }).ToList();

        var dict = allTaskDtos.ToDictionary(t => t.Id);
        var rootTasks = new System.Collections.Generic.List<PlanTaskDto>();

        foreach (var task in allTaskDtos.OrderBy(t => t.OrderIndex))
        {
            if (task.ParentTaskId.HasValue && dict.TryGetValue(task.ParentTaskId.Value, out var parent))
            {
                parent.SubTasks.Add(task);
            }
            else
            {
                rootTasks.Add(task);
            }
        }

        return rootTasks;
    }
}
