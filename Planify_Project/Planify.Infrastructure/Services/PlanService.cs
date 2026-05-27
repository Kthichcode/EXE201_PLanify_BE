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

        await RecalculatePlanProgressAsync(plan.Id);

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

    public async Task<PlanTaskDto> UpdateTaskStatusAsync(Guid planId, Guid taskId, UpdateTaskStatusDto dto, Guid userId)
    {
        var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId);
        if (plan == null) throw new Exception("Plan not found or access denied.");

        var task = await _context.PlanTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.PlanId == planId);
        if (task == null) throw new Exception("Task not found.");

        task.Status = dto.Status;
        if (dto.Status == "done")
        {
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            if (task.Progress == 100) task.Progress = 0;
            task.CompletedAt = null;
        }
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await RecalculatePlanProgressAsync(planId);

        return new PlanTaskDto
        {
            Id = task.Id,
            PlanId = task.PlanId,
            ParentTaskId = task.ParentTaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            Progress = task.Progress,
            OrderIndex = task.OrderIndex,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }

    private async Task RecalculatePlanProgressAsync(Guid planId)
    {
        var plan = await _context.Plans.Include(p => p.Tasks).FirstOrDefaultAsync(p => p.Id == planId);
        if (plan == null) return;

        var tasks = plan.Tasks;
        if (tasks == null || !tasks.Any())
        {
            plan.Progress = 0;
            await _context.SaveChangesAsync();
            return;
        }

        var level1Tasks = tasks.Where(t => t.ParentTaskId == null).ToList();
        var subtasks = tasks.Where(t => t.ParentTaskId != null).ToList();

        foreach (var parent in level1Tasks)
        {
            var children = subtasks.Where(t => t.ParentTaskId == parent.Id).ToList();
            if (children.Any())
            {
                parent.Progress = (int)children.Average(c => c.Progress);
                if (parent.Progress == 100 && parent.Status != "done")
                {
                    parent.Status = "done";
                    parent.CompletedAt = DateTime.UtcNow;
                }
                else if (parent.Progress < 100 && parent.Status == "done")
                {
                    parent.Status = "in_progress";
                    parent.CompletedAt = null;
                }
            }
        }

        if (level1Tasks.Any())
        {
            plan.Progress = (int)level1Tasks.Average(t => t.Progress);
            if (plan.Progress == 100 && plan.Status != "done") plan.Status = "done";
            else if (plan.Progress < 100 && plan.Status == "done") plan.Status = "in_progress";
        }

        await _context.SaveChangesAsync();
    }

    public async Task<PlanDto?> GetPlanByIdAsync(Guid planId, Guid userId)
    {
        var plan = await _context.Plans
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId);

        if (plan == null) return null;

        return MapToDto(plan);
    }

    public async Task<System.Collections.Generic.List<PlanDto>> GetPlansByUserIdAsync(Guid userId)
    {
        var plans = await _context.Plans
            .Include(p => p.Tasks)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return plans.Select(MapToDto).ToList();
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
