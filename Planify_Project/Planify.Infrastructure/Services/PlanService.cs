using Planify.Application.DTOs.AI;
using Planify.Application.DTOs.Plans;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;

namespace Planify.Infrastructure.Services;

public class PlanService : IPlanService
{
    private readonly IPlanRepository     _planRepo;
    private readonly IPlanTaskRepository _taskRepo;

    public PlanService(IPlanRepository planRepo, IPlanTaskRepository taskRepo)
    {
        _planRepo = planRepo;
        _taskRepo = taskRepo;
    }

    // ── CreateManualPlan ─────────────────────────────────────────────────────

    public async Task<PlanDto> CreateManualPlanAsync(CreatePlanDto dto, Guid userId)
    {
        var plan = new Plan
        {
            UserId        = userId,
            TemplateId    = dto.TemplateId,
            FrameworkId   = dto.FrameworkId,
            CategoryId    = dto.CategoryId,
            Title         = dto.Title,
            Description   = dto.Description,
            Goal          = dto.Goal,
            IsPublic      = dto.IsPublic,
            Deadline      = dto.Deadline,
            Status        = "active",
            Progress      = 0,
            IsAIGenerated = false,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };

        await _planRepo.AddAsync(plan);
        await _planRepo.SaveChangesAsync();

        return MapToDto(plan);
    }

    // ── AddTaskToPlan ────────────────────────────────────────────────────────

    public async Task<PlanTaskDto> AddTaskToPlanAsync(Guid planId, CreatePlanTaskDto dto, Guid userId)
    {
        var plan = await _planRepo.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId)
            throw new Exception("Plan not found or you do not have permission to access it.");

        if (dto.ParentTaskId.HasValue)
        {
            var tasks = await _taskRepo.GetByPlanIdAsync(planId);
            if (!tasks.Any(t => t.Id == dto.ParentTaskId.Value))
                throw new Exception("Parent task not found in this plan.");
        }

        var planTask = new PlanTask
        {
            PlanId       = plan.Id,
            ParentTaskId = dto.ParentTaskId,
            Title        = dto.Title,
            Description  = dto.Description,
            Priority     = dto.Priority ?? "medium",
            StartDate    = dto.StartDate,
            DueDate      = dto.DueDate,
            OrderIndex   = dto.OrderIndex,
            Status       = "todo",
            Progress     = 0,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };

        await _taskRepo.AddAsync(planTask);
        await _taskRepo.SaveChangesAsync();
        await RecalculatePlanProgressAsync(planId);

        return MapTaskToDto(planTask);
    }

    // ── UpdateTaskStatus ─────────────────────────────────────────────────────

    public async Task<PlanTaskDto> UpdateTaskStatusAsync(Guid planId, Guid taskId, UpdateTaskStatusDto dto, Guid userId)
    {
        var plan = await _planRepo.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId)
            throw new Exception("Plan not found or access denied.");

        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task == null || task.PlanId != planId)
            throw new Exception("Task not found.");

        task.Status = dto.Status;
        if (dto.Status == "done")
        {
            task.Progress    = 100;
            task.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            if (task.Progress == 100) task.Progress = 0;
            task.CompletedAt = null;
        }
        task.UpdatedAt = DateTime.UtcNow;

        await _taskRepo.SaveChangesAsync();
        await RecalculatePlanProgressAsync(planId);

        return MapTaskToDto(task);
    }

    // ── GetPlanById ──────────────────────────────────────────────────────────

    public async Task<PlanDto?> GetPlanByIdAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepo.GetByIdWithTasksAsync(planId);
        if (plan == null || plan.UserId != userId) return null;
        return MapToDto(plan);
    }

    // ── GetPlansByUserId ─────────────────────────────────────────────────────

    public async Task<List<PlanDto>> GetPlansByUserIdAsync(Guid userId)
    {
        var plans = await _planRepo.GetByUserIdAsync(userId);
        return plans.Select(MapToDto).ToList();
    }

    // ── SaveAiPlanAsDraft ────────────────────────────────────────────────────

    public async Task<PlanDto> SaveAiPlanAsDraftAsync(SaveAiPlanRequestDto dto, Guid userId)
    {
        var planData = dto.PlanData;
        var planNode = planData["plan"]
            ?? throw new InvalidOperationException("planData thiếu trường 'plan'.");

        DateTime? deadline = null;
        if (planNode["Deadline"]?.GetValue<string>() is { } dl && !string.IsNullOrWhiteSpace(dl))
            deadline = DateTime.Parse(dl);

        var plan = new Plan
        {
            UserId         = userId,
            Title          = planNode["Title"]?.GetValue<string>() ?? "Kế hoạch AI",
            Description    = planNode["Description"]?.GetValue<string>(),
            Goal           = planNode["Goal"]?.GetValue<string>(),
            Deadline       = deadline,
            Status         = "draft",
            Progress       = 0,
            IsAIGenerated  = true,
            IsPublic       = planNode["IsPublic"]?.GetValue<bool>() ?? false,
            DraftExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow
        };

        var taskNodes = planData["tasks"]?.AsArray()
            ?? throw new InvalidOperationException("planData thiếu trường 'tasks'.");

        var planTasks = new List<PlanTask>();
        foreach (var taskNode in taskNodes)
        {
            if (taskNode is null) continue;

            var task = new PlanTask
            {
                Title       = taskNode["Title"]?.GetValue<string>() ?? "Task",
                Description = taskNode["Description"]?.GetValue<string>(),
                Priority    = taskNode["Priority"]?.GetValue<string>() ?? "medium",
                Status      = "todo",
                StartDate   = ParseDate(taskNode["StartDate"]?.GetValue<string>()),
                DueDate     = ParseDate(taskNode["DueDate"]?.GetValue<string>()),
                Progress    = 0,
                OrderIndex  = taskNode["OrderIndex"]?.GetValue<int>() ?? 0,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            };

            var subtaskNodes = taskNode["subtasks"]?.AsArray();
            if (subtaskNodes is not null)
            {
                foreach (var stNode in subtaskNodes)
                {
                    if (stNode is null) continue;
                    task.SubTasks.Add(new PlanTask
                    {
                        Title       = stNode["Title"]?.GetValue<string>() ?? "Subtask",
                        Description = stNode["Description"]?.GetValue<string>(),
                        Priority    = stNode["Priority"]?.GetValue<string>() ?? "medium",
                        Status      = "todo",
                        StartDate   = ParseDate(stNode["StartDate"]?.GetValue<string>()),
                        DueDate     = ParseDate(stNode["DueDate"]?.GetValue<string>()),
                        Progress    = 0,
                        OrderIndex  = stNode["OrderIndex"]?.GetValue<int>() ?? 0,
                        CreatedAt   = DateTime.UtcNow,
                        UpdatedAt   = DateTime.UtcNow
                    });
                }
            }

            planTasks.Add(task);
        }

        await _planRepo.ExecuteInTransactionAsync(async () =>
        {
            await _planRepo.AddAsync(plan);
            await _planRepo.SaveChangesAsync();

            foreach (var task in planTasks)
            {
                task.PlanId = plan.Id;
                var subtasks = task.SubTasks.ToList();
                task.SubTasks.Clear();

                await _taskRepo.AddAsync(task);
                await _taskRepo.SaveChangesAsync();

                foreach (var st in subtasks)
                {
                    st.PlanId       = plan.Id;
                    st.ParentTaskId = task.Id;
                    await _taskRepo.AddAsync(st);
                }
                await _taskRepo.SaveChangesAsync();
            }
        });

        var saved = await _planRepo.GetByIdWithTasksAsync(plan.Id)
            ?? throw new InvalidOperationException("Không thể load plan sau khi lưu.");

        return MapToDto(saved);
    }

    // ── ConfirmDraftPlan ─────────────────────────────────────────────────────

    public async Task<PlanDto> ConfirmDraftPlanAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepo.GetByIdWithTasksAsync(planId);

        if (plan is null || plan.UserId != userId)
            throw new KeyNotFoundException("Không tìm thấy kế hoạch hoặc bạn không có quyền truy cập.");

        if (plan.Status != "draft")
            throw new InvalidOperationException($"Kế hoạch không ở trạng thái draft (hiện tại: {plan.Status}).");

        plan.Status         = "active";
        plan.DraftExpiresAt = null;
        plan.UpdatedAt      = DateTime.UtcNow;

        await _planRepo.SaveChangesAsync();
        return MapToDto(plan);
    }

    // ── DiscardDraftPlan ─────────────────────────────────────────────────────

    public async Task DiscardDraftPlanAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepo.GetByIdAsync(planId);

        if (plan is null || plan.UserId != userId)
            throw new KeyNotFoundException("Không tìm thấy kế hoạch.");

        if (plan.Status != "draft")
            throw new InvalidOperationException("Chỉ có thể xóa kế hoạch ở trạng thái draft.");

        await _planRepo.DeleteAsync(plan);
        await _planRepo.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task RecalculatePlanProgressAsync(Guid planId)
    {
        var plan = await _planRepo.GetByIdWithTasksAsync(planId);
        if (plan == null) return;

        var tasks = plan.Tasks;
        if (tasks == null || !tasks.Any())
        {
            plan.Progress = 0;
            await _planRepo.SaveChangesAsync();
            return;
        }

        var level1Tasks = tasks.Where(t => t.ParentTaskId == null).ToList();
        var subtasks    = tasks.Where(t => t.ParentTaskId != null).ToList();

        foreach (var parent in level1Tasks)
        {
            var children = subtasks.Where(t => t.ParentTaskId == parent.Id).ToList();
            if (children.Any())
            {
                parent.Progress = (int)children.Average(c => c.Progress);
                if (parent.Progress == 100 && parent.Status != "done")
                {
                    parent.Status      = "done";
                    parent.CompletedAt = DateTime.UtcNow;
                }
                else if (parent.Progress < 100 && parent.Status == "done")
                {
                    parent.Status      = "in_progress";
                    parent.CompletedAt = null;
                }
            }
        }

        if (level1Tasks.Any())
        {
            plan.Progress = (int)level1Tasks.Average(t => t.Progress);
            if (plan.Progress == 100 && plan.Status != "done")     plan.Status = "done";
            else if (plan.Progress < 100 && plan.Status == "done") plan.Status = "in_progress";
        }

        await _planRepo.SaveChangesAsync();
    }

    private static PlanDto MapToDto(Plan plan) => new()
    {
        Id             = plan.Id,
        UserId         = plan.UserId,
        TemplateId     = plan.TemplateId,
        FrameworkId    = plan.FrameworkId,
        CategoryId     = plan.CategoryId,
        Title          = plan.Title,
        Description    = plan.Description,
        Goal           = plan.Goal,
        Status         = plan.Status,
        IsPublic       = plan.IsPublic,
        Deadline       = plan.Deadline,
        Progress       = plan.Progress,
        IsAIGenerated  = plan.IsAIGenerated,
        SortOrder      = plan.SortOrder,
        DraftExpiresAt = plan.DraftExpiresAt,
        CreatedAt      = plan.CreatedAt,
        UpdatedAt      = plan.UpdatedAt,
        Tasks          = BuildTaskTree(plan.Tasks)
    };

    private static PlanTaskDto MapTaskToDto(PlanTask t) => new()
    {
        Id           = t.Id,
        PlanId       = t.PlanId,
        ParentTaskId = t.ParentTaskId,
        Title        = t.Title,
        Description  = t.Description,
        Status       = t.Status,
        Priority     = t.Priority,
        StartDate    = t.StartDate,
        DueDate      = t.DueDate,
        CompletedAt  = t.CompletedAt,
        Progress     = t.Progress,
        OrderIndex   = t.OrderIndex,
        CreatedAt    = t.CreatedAt,
        UpdatedAt    = t.UpdatedAt
    };

    private static List<PlanTaskDto> BuildTaskTree(ICollection<PlanTask>? tasks)
    {
        if (tasks == null || !tasks.Any()) return new List<PlanTaskDto>();

        var allDtos = tasks.Select(MapTaskToDto).ToList();
        var dict    = allDtos.ToDictionary(t => t.Id);
        var roots   = new List<PlanTaskDto>();

        foreach (var task in allDtos.OrderBy(t => t.OrderIndex))
        {
            if (task.ParentTaskId.HasValue && dict.TryGetValue(task.ParentTaskId.Value, out var parent))
                parent.SubTasks.Add(task);
            else
                roots.Add(task);
        }

        return roots;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var d) ? d : null;
    }
}
