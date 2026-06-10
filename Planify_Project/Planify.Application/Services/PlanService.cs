using Planify.Application.DTOs.AI;
using Planify.Application.DTOs.Plans;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Application.Services;

public class PlanService : IPlanService
{
    private readonly IPlanRepository _planRepository;
    private readonly IPlanTaskRepository _taskRepository;
    private readonly ISubscriptionGuardService _guard;

    public PlanService(
        IPlanRepository planRepository,
        IPlanTaskRepository taskRepository,
        ISubscriptionGuardService guard)
    {
        _planRepository = planRepository;
        _taskRepository = taskRepository;
        _guard          = guard;
    }

    public async Task<PlanDto> CreateManualPlanAsync(CreatePlanDto dto, Guid userId)
    {
        // Kiểm tra giới hạn số plan trước khi tạo
        await _guard.EnforceMaxPlansAsync(userId);

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

        await _planRepository.AddAsync(plan);
        await _planRepository.SaveChangesAsync();

        return MapToDto(plan);
    }

    public async Task<PlanTaskDto> AddTaskToPlanAsync(Guid planId, CreatePlanTaskDto dto, Guid userId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId)
        {
            throw new Exception("Plan not found or you do not have permission to access it.");
        }

        if (dto.ParentTaskId.HasValue)
        {
            var parent = await _taskRepository.GetByIdAsync(dto.ParentTaskId.Value);
            if (parent == null || parent.PlanId != plan.Id)
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

        await _taskRepository.AddAsync(planTask);
        await _taskRepository.SaveChangesAsync();

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
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId) throw new Exception("Plan not found or access denied.");

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null || task.PlanId != planId) throw new Exception("Task not found.");

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

        await _taskRepository.SaveChangesAsync();
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

    public async Task<PlanDto> SaveAiPlanAsDraftAsync(SaveAiPlanRequestDto dto, Guid userId)
    {
        // Kiểm tra giới hạn lượt tạo AI plan + số plan tối đa
        await _guard.EnforceAndConsumeAiGenerateAsync(userId);
        await _guard.EnforceMaxPlansAsync(userId);

        var planData = dto.PlanData;

        var planNode = planData["plan"]
            ?? throw new InvalidOperationException("planData thiếu trường 'plan'.");

        // ── Helper đọc field case-insensitive (AI có thể trả về Title hoặc title) ──
        static string? GetStr(System.Text.Json.Nodes.JsonNode node, string key)
        {
            // Thử PascalCase trước (schema chuẩn), rồi lowercase
            var val = node[key]?.GetValue<string>()
                   ?? node[char.ToLower(key[0]) + key[1..]]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }

        static DateTime? GetDate(System.Text.Json.Nodes.JsonNode node, string key)
        {
            var raw = node[key]?.GetValue<string>()
                   ?? node[char.ToLower(key[0]) + key[1..]]?.GetValue<string>();
            return DateTime.TryParse(raw, out var d) ? d : null;
        }

        // ── Validate Plan node ────────────────────────────────────────────────
        var planTitle = GetStr(planNode, "Title")
            ?? throw new InvalidOperationException("AI không trả về Title cho plan. Vui lòng thử lại.");

        var planDesc = GetStr(planNode, "Description")
            ?? throw new InvalidOperationException("AI không trả về Description cho plan. Vui lòng thử lại.");

        // ── Build Plan entity ─────────────────────────────────────────────────
        var plan = new Plan
        {
            UserId         = userId,
            TemplateId     = dto.TemplateId,
            FrameworkId    = dto.FrameworkId,
            Title          = planTitle,
            Description    = planDesc,
            Goal           = GetStr(planNode, "Goal"),
            Deadline       = GetDate(planNode, "Deadline"),
            Status         = "draft",
            IsAIGenerated  = true,
            IsPublic       = planNode["IsPublic"]?.GetValue<bool>() ?? false,
            Progress       = 0,
            DraftExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow
        };

        // ── Validate Tasks array ──────────────────────────────────────────────
        var tasksNode = (planData["tasks"] ?? planData["Tasks"]) as System.Text.Json.Nodes.JsonArray
            ?? throw new InvalidOperationException("AI không trả về danh sách tasks. Vui lòng thử lại.");

        if (tasksNode.Count == 0)
            throw new InvalidOperationException("AI trả về danh sách tasks rỗng. Vui lòng thử lại.");

        // ── Build Task entities ───────────────────────────────────────────────
        var planTasks = new List<PlanTask>();
        int taskOrder = 1;

        foreach (var tNode in tasksNode)
        {
            if (tNode is null) continue;

            var taskTitle = GetStr(tNode, "Title")
                ?? throw new InvalidOperationException($"Task #{taskOrder} không có Title. Vui lòng thử lại.");

            var taskDesc = GetStr(tNode, "Description")
                ?? throw new InvalidOperationException($"Task '{taskTitle}' không có Description. Vui lòng thử lại.");

            var task = new PlanTask
            {
                Title       = taskTitle,
                Description = taskDesc,
                Priority    = GetStr(tNode, "Priority") ?? "medium",
                Status      = "todo",
                StartDate   = GetDate(tNode, "StartDate"),
                DueDate     = GetDate(tNode, "DueDate"),
                Progress    = 0,
                OrderIndex  = tNode["OrderIndex"]?.GetValue<int>() ?? taskOrder,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            };

            // Subtasks — schema AI dùng "subtasks" (lowercase)
            var subNode = (tNode["subtasks"] ?? tNode["subTasks"] ?? tNode["Subtasks"])
                          as System.Text.Json.Nodes.JsonArray;

            int subOrder = 1;
            if (subNode is not null)
            {
                foreach (var stNode in subNode)
                {
                    if (stNode is null) continue;

                    var subTitle = GetStr(stNode, "Title")
                        ?? throw new InvalidOperationException($"Subtask #{subOrder} của task '{taskTitle}' không có Title.");

                    var subDesc = GetStr(stNode, "Description")
                        ?? throw new InvalidOperationException($"Subtask '{subTitle}' không có Description.");

                    task.SubTasks.Add(new PlanTask
                    {
                        Title       = subTitle,
                        Description = subDesc,
                        Priority    = GetStr(stNode, "Priority") ?? "medium",
                        Status      = "todo",
                        StartDate   = GetDate(stNode, "StartDate"),
                        DueDate     = GetDate(stNode, "DueDate"),
                        Progress    = 0,
                        OrderIndex  = stNode["OrderIndex"]?.GetValue<int>() ?? subOrder,
                        CreatedAt   = DateTime.UtcNow,
                        UpdatedAt   = DateTime.UtcNow
                    });
                    subOrder++;
                }
            }

            planTasks.Add(task);
            taskOrder++;
        }

        // ── Lưu vào DB trong 1 transaction ───────────────────────────────────
        await _planRepository.ExecuteInTransactionAsync(async () =>
        {
            await _planRepository.AddAsync(plan);
            await _planRepository.SaveChangesAsync();

            foreach (var task in planTasks)
            {
                task.PlanId = plan.Id;
                var subtasks = task.SubTasks.ToList();
                task.SubTasks.Clear();

                await _taskRepository.AddAsync(task);
                await _taskRepository.SaveChangesAsync();

                foreach (var st in subtasks)
                {
                    st.PlanId       = plan.Id;
                    st.ParentTaskId = task.Id;
                    await _taskRepository.AddAsync(st);
                }
                await _taskRepository.SaveChangesAsync();
            }
        });

        return await GetPlanByIdAsync(plan.Id, userId)
            ?? throw new InvalidOperationException("Không thể load plan sau khi lưu.");
    }

    public async Task<PlanDto> ConfirmDraftPlanAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId) throw new Exception("Plan not found or access denied.");
        
        if (plan.Status != "draft") throw new Exception("Plan is not a draft.");

        plan.Status = "active";
        plan.UpdatedAt = DateTime.UtcNow;
        
        await _planRepository.SaveChangesAsync();
        return await GetPlanByIdAsync(planId, userId) ?? throw new Exception("Failed to retrieve confirmed plan.");
    }

    public async Task DiscardDraftPlanAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId) throw new Exception("Plan not found or access denied.");
        
        if (plan.Status != "draft") throw new Exception("Only draft plans can be discarded.");

        await _planRepository.DeleteAsync(plan);
        await _planRepository.SaveChangesAsync();
    }

    public async Task<PlanDto> RefreshDraftWithRefinedPlanAsync(Guid planId, SaveAiPlanRequestDto dto, Guid userId)
    {
        // Kiểm tra giới hạn lượt refine AI plan
        await _guard.EnforceAndConsumeAiRefineAsync(userId);

        // ── 1. Load plan, kiểm tra quyền và trạng thái ──────────────────────
        var plan = await _planRepository.GetByIdWithTasksAsync(planId)
            ?? throw new KeyNotFoundException($"Không tìm thấy plan với id={planId}.");

        if (plan.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa kế hoạch này.");

        if (plan.Status == "discarded")
            throw new InvalidOperationException("Không thể chỉnh sửa kế hoạch đã bị hủy.");

        var planData = dto.PlanData;

        // ── Helper đọc field case-insensitive ───────────────────────────────
        static string? GetStr(System.Text.Json.Nodes.JsonNode node, string key)
        {
            var val = node[key]?.GetValue<string>()
                   ?? node[char.ToLower(key[0]) + key[1..]]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }

        static DateTime? GetDate(System.Text.Json.Nodes.JsonNode node, string key)
        {
            var raw = node[key]?.GetValue<string>()
                   ?? node[char.ToLower(key[0]) + key[1..]]?.GetValue<string>();
            return DateTime.TryParse(raw, out var d) ? d : null;
        }

        // ── 2. Parse Plan node ───────────────────────────────────────────────
        var planNode = planData["plan"]
            ?? throw new InvalidOperationException("planData thiếu trường 'plan'.");

        var planTitle = GetStr(planNode, "Title")
            ?? throw new InvalidOperationException("AI không trả về Title cho plan. Vui lòng thử lại.");

        var planDesc = GetStr(planNode, "Description")
            ?? throw new InvalidOperationException("AI không trả về Description cho plan. Vui lòng thử lại.");

        // ── 3. Parse Tasks array ─────────────────────────────────────────────
        var tasksNode = (planData["tasks"] ?? planData["Tasks"]) as System.Text.Json.Nodes.JsonArray
            ?? throw new InvalidOperationException("AI không trả về danh sách tasks. Vui lòng thử lại.");

        if (tasksNode.Count == 0)
            throw new InvalidOperationException("AI trả về danh sách tasks rỗng. Vui lòng thử lại.");

        // ── 4. Overwrite trong transaction ───────────────────────────────────
        await _planRepository.ExecuteInTransactionAsync(async () =>
        {
            // 4a. Xóa toàn bộ tasks/subtasks cũ
            if (plan.Tasks != null)
            {
                var allTasks = plan.Tasks.ToList();
                foreach (var t in allTasks)
                    await _taskRepository.DeleteAsync(t);
                await _taskRepository.SaveChangesAsync();
            }

            // 4b. Cập nhật thông tin plan từ AI mới
            plan.Title          = planTitle;
            plan.Description    = planDesc;
            plan.Goal           = GetStr(planNode, "Goal") ?? plan.Goal;
            plan.Deadline       = GetDate(planNode, "Deadline") ?? plan.Deadline;
            plan.IsPublic       = planNode["IsPublic"]?.GetValue<bool>() ?? plan.IsPublic;
            plan.Progress       = 0;
            plan.UpdatedAt      = DateTime.UtcNow;

            // Gia hạn thêm 24h nếu là draft, không đụng DraftExpiresAt nếu đã active
            if (plan.Status == "draft")
                plan.DraftExpiresAt = DateTime.UtcNow.AddHours(24);

            await _planRepository.SaveChangesAsync();

            // 4c. Insert tasks/subtasks mới
            int taskOrder = 1;
            foreach (var tNode in tasksNode)
            {
                if (tNode is null) continue;

                var taskTitle = GetStr(tNode, "Title")
                    ?? throw new InvalidOperationException($"Task #{taskOrder} không có Title. Vui lòng thử lại.");

                var taskDesc = GetStr(tNode, "Description")
                    ?? throw new InvalidOperationException($"Task '{taskTitle}' không có Description. Vui lòng thử lại.");

                var task = new PlanTask
                {
                    PlanId      = plan.Id,
                    Title       = taskTitle,
                    Description = taskDesc,
                    Priority    = GetStr(tNode, "Priority") ?? "medium",
                    Status      = "todo",
                    StartDate   = GetDate(tNode, "StartDate"),
                    DueDate     = GetDate(tNode, "DueDate"),
                    Progress    = 0,
                    OrderIndex  = tNode["OrderIndex"]?.GetValue<int>() ?? taskOrder,
                    CreatedAt   = DateTime.UtcNow,
                    UpdatedAt   = DateTime.UtcNow
                };

                var subNode = (tNode["subtasks"] ?? tNode["subTasks"] ?? tNode["Subtasks"])
                              as System.Text.Json.Nodes.JsonArray;

                await _taskRepository.AddAsync(task);
                await _taskRepository.SaveChangesAsync();

                int subOrder = 1;
                if (subNode is not null)
                {
                    foreach (var stNode in subNode)
                    {
                        if (stNode is null) continue;

                        var subTitle = GetStr(stNode, "Title")
                            ?? throw new InvalidOperationException($"Subtask #{subOrder} của task '{taskTitle}' không có Title.");

                        var subDesc = GetStr(stNode, "Description")
                            ?? throw new InvalidOperationException($"Subtask '{subTitle}' không có Description.");

                        var subtask = new PlanTask
                        {
                            PlanId       = plan.Id,
                            ParentTaskId = task.Id,
                            Title        = subTitle,
                            Description  = subDesc,
                            Priority     = GetStr(stNode, "Priority") ?? "medium",
                            Status       = "todo",
                            StartDate    = GetDate(stNode, "StartDate"),
                            DueDate      = GetDate(stNode, "DueDate"),
                            Progress     = 0,
                            OrderIndex   = stNode["OrderIndex"]?.GetValue<int>() ?? subOrder,
                            CreatedAt    = DateTime.UtcNow,
                            UpdatedAt    = DateTime.UtcNow
                        };

                        await _taskRepository.AddAsync(subtask);
                        subOrder++;
                    }
                    await _taskRepository.SaveChangesAsync();
                }

                taskOrder++;
            }
        });

        return await GetPlanByIdAsync(plan.Id, userId)
            ?? throw new InvalidOperationException("Không thể load plan sau khi chỉnh sửa.");
    }

    private async Task RecalculatePlanProgressAsync(Guid planId)
    {
        var plan = await _planRepository.GetByIdWithTasksAsync(planId);
        if (plan == null) return;

        var tasks = plan.Tasks;
        if (tasks == null || !tasks.Any())
        {
            plan.Progress = 0;
            await _planRepository.SaveChangesAsync();
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

        await _planRepository.SaveChangesAsync();
    }

    public async Task<PlanDto?> GetPlanByIdAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepository.GetByIdWithTasksAsync(planId);
        if (plan == null || plan.UserId != userId) return null;

        return MapToDto(plan);
    }

    public async Task<System.Collections.Generic.List<PlanDto>> GetPlansByUserIdAsync(Guid userId)
    {
        var plans = await _planRepository.GetByUserIdAsync(userId);
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

    public async Task DeletePlanAsync(Guid planId, Guid userId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId) throw new KeyNotFoundException("Plan not found or access denied.");

        await _planRepository.DeleteAsync(plan);
        await _planRepository.SaveChangesAsync();
    }

    public async Task<PlanTaskDto> UpdatePlanTaskAsync(Guid planId, Guid taskId, UpdatePlanTaskDto dto, Guid userId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId) throw new Exception("Plan not found or access denied.");

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null || task.PlanId != planId) throw new Exception("Task not found.");

        task.Title = dto.Title;
        task.Description = dto.Description;
        task.Priority = dto.Priority ?? task.Priority;
        task.StartDate = dto.StartDate;
        task.DueDate = dto.DueDate;
        task.UpdatedAt = DateTime.UtcNow;

        await _taskRepository.SaveChangesAsync();
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

    public async Task DeletePlanTaskAsync(Guid planId, Guid taskId, Guid userId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null || plan.UserId != userId) throw new Exception("Plan not found or access denied.");

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null || task.PlanId != planId) throw new Exception("Task not found.");

        // Nếu là task cha, xóa cả các task con
        var allTasks = await _taskRepository.GetByPlanIdAsync(planId);
        var subtasks = allTasks.Where(t => t.ParentTaskId == taskId).ToList();
        if (subtasks.Any())
        {
            await _taskRepository.DeleteRangeAsync(subtasks);
        }

        await _taskRepository.DeleteAsync(task);
        await _taskRepository.SaveChangesAsync();

        await RecalculatePlanProgressAsync(planId);
    }
}
