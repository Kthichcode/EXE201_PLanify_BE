using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Community;
using Planify.Application.DTOs.Plans;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Application.Services;

public class CommunityPlanService : ICommunityPlanService
{
    private readonly ICommunityPlanRepository _communityPlanRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IPlanTaskRepository _taskRepository;
    private readonly ISubscriptionGuardService _guard;

    public CommunityPlanService(
        ICommunityPlanRepository communityPlanRepository,
        IPlanRepository planRepository,
        IPlanTaskRepository taskRepository,
        ISubscriptionGuardService guard)
    {
        _communityPlanRepository = communityPlanRepository;
        _planRepository = planRepository;
        _taskRepository = taskRepository;
        _guard          = guard;
    }

    // ── Publish ─────────────────────────────────────────────────────────────

    public async Task<CommunityPlanDto> PublishPlanAsync(PublishPlanDto dto, Guid userId)
    {
        // Kiểm tra quyền publish (Premium+ only)
        await _guard.EnforcePublishPlanAsync(userId);
        // 1. Kiểm tra plan tồn tại và thuộc user
        var plan = await _planRepository.GetByIdWithTasksAsync(dto.PlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy plan.");

        if (plan.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền publish plan này.");

        if (plan.Status == "draft")
            throw new InvalidOperationException("Không thể publish plan đang ở trạng thái draft. Vui lòng xác nhận plan trước.");

        // 2. Kiểm tra plan chưa được publish
        var alreadyPublished = await _communityPlanRepository.ExistsByPlanIdAsync(dto.PlanId);
        if (alreadyPublished)
            throw new InvalidOperationException("Plan này đã được publish lên thư viện.");

        // 3. Tạo CommunityPlan (status = pending)
        var communityPlan = new CommunityPlan
        {
            PlanId      = dto.PlanId,
            UserId      = userId,
            CategoryId  = dto.CategoryId,
            Title       = dto.Title,
            Description = dto.Description,
            Status      = "pending",
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        await _communityPlanRepository.AddAsync(communityPlan);
        await _communityPlanRepository.SaveChangesAsync();

        return MapToDto(communityPlan, null, false);
    }

    // ── Browse library ───────────────────────────────────────────────────────

    public async Task<PagedResultDto<CommunityPlanSummaryDto>> GetLibraryAsync(
        CommunityPlanQueryDto query, Guid? currentUserId)
    {
        var (items, totalCount) = await _communityPlanRepository.GetApprovedPagedAsync(
            query.Search,
            query.CategoryId,
            query.SortBy,
            query.Page,
            query.PageSize);

        // Nếu user đã đăng nhập, xác định những plan user đã like
        HashSet<Guid> likedIds = new();
        if (currentUserId.HasValue)
        {
            foreach (var item in items)
            {
                var like = await _communityPlanRepository.GetLikeAsync(item.Id, currentUserId.Value);
                if (like != null) likedIds.Add(item.Id);
            }
        }

        var summaries = items.Select(cp => MapToSummaryDto(cp,
            currentUserId.HasValue ? likedIds.Contains(cp.Id) : null)).ToList();

        return new PagedResultDto<CommunityPlanSummaryDto>
        {
            Items      = summaries,
            TotalCount = totalCount,
            Page       = query.Page,
            PageSize   = query.PageSize
        };
    }

    public async Task<CommunityPlanDto?> GetCommunityPlanByIdAsync(Guid id, Guid? currentUserId)
    {
        var cp = await _communityPlanRepository.GetByIdWithDetailsAsync(id);
        if (cp == null) return null;

        // Chỉ cho phép xem approved plan (hoặc owner xem plan của mình)
        if (cp.Status != "approved" && cp.UserId != currentUserId)
            return null;

        bool? isLiked = null;
        if (currentUserId.HasValue)
        {
            var like = await _communityPlanRepository.GetLikeAsync(id, currentUserId.Value);
            isLiked = like != null;
        }

        var dto = MapToDto(cp, isLiked, true);
        return dto;
    }

    // ── Copy plan ────────────────────────────────────────────────────────────

    public async Task<PlanDto> CopyPlanToUserAsync(Guid communityPlanId, Guid userId)
    {
        // Kiểm tra quyền copy (Premium+ only) + giới hạn số plan
        await _guard.EnforceCommunityPlanCopyAsync(userId);
        await _guard.EnforceMaxPlansAsync(userId);
        var cp = await _communityPlanRepository.GetByIdWithDetailsAsync(communityPlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy community plan.");

        if (cp.Status != "approved")
            throw new InvalidOperationException("Chỉ có thể copy plan đã được duyệt.");

        // Load plan gốc kèm tasks
        var sourcePlan = await _planRepository.GetByIdWithTasksAsync(cp.PlanId)
            ?? throw new InvalidOperationException("Plan gốc không còn tồn tại.");

        Plan? newPlan = null;

        await _communityPlanRepository.ExecuteInTransactionAsync(async () =>
        {
            // 1. Clone Plan
            newPlan = new Plan
            {
                UserId        = userId,
                TemplateId    = sourcePlan.TemplateId,
                FrameworkId   = sourcePlan.FrameworkId,
                CategoryId    = sourcePlan.CategoryId,
                Title         = sourcePlan.Title,
                Description   = sourcePlan.Description,
                Goal          = sourcePlan.Goal,
                Status        = "active",
                IsPublic      = false,
                IsAIGenerated = sourcePlan.IsAIGenerated,
                Progress      = 0,
                CreatedAt     = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow
            };

            await _planRepository.AddAsync(newPlan);
            await _planRepository.SaveChangesAsync();

            // 2. Clone Tasks + SubTasks
            var rootTasks = sourcePlan.Tasks
                .Where(t => t.ParentTaskId == null)
                .OrderBy(t => t.OrderIndex)
                .ToList();

            foreach (var sourceTask in rootTasks)
            {
                var newTask = CloneTask(sourceTask, newPlan.Id, null);
                await _taskRepository.AddAsync(newTask);
                await _taskRepository.SaveChangesAsync();

                // SubTasks
                var subTasks = sourcePlan.Tasks
                    .Where(t => t.ParentTaskId == sourceTask.Id)
                    .OrderBy(t => t.OrderIndex)
                    .ToList();

                foreach (var sourceSub in subTasks)
                {
                    var newSub = CloneTask(sourceSub, newPlan.Id, newTask.Id);
                    await _taskRepository.AddAsync(newSub);
                }
                await _taskRepository.SaveChangesAsync();
            }

            // 3. Tạo PlanCopy record
            var copy = new PlanCopy
            {
                CommunityPlanId = communityPlanId,
                UserId          = userId,
                NewPlanId       = newPlan.Id,
                CreatedAt       = DateTime.UtcNow
            };
            await _communityPlanRepository.AddCopyAsync(copy);

            // 4. Tăng DownloadCount
            cp.DownloadCount++;
            cp.UpdatedAt = DateTime.UtcNow;

            await _communityPlanRepository.SaveChangesAsync();
        });

        // Load lại plan mới để trả về đầy đủ
        var result = await _planRepository.GetByIdWithTasksAsync(newPlan!.Id)
            ?? throw new InvalidOperationException("Không thể load plan vừa tạo.");

        return MapPlanToDto(result);
    }

    // ── Like / Unlike ────────────────────────────────────────────────────────

    public async Task<bool> ToggleLikeAsync(Guid communityPlanId, Guid userId)
    {
        var cp = await _communityPlanRepository.GetByIdAsync(communityPlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy community plan.");

        if (cp.Status != "approved")
            throw new InvalidOperationException("Chỉ có thể like plan đã được duyệt.");

        var existingLike = await _communityPlanRepository.GetLikeAsync(communityPlanId, userId);

        if (existingLike != null)
        {
            // Unlike
            await _communityPlanRepository.RemoveLikeAsync(existingLike);
            cp.LikeCount = Math.Max(0, cp.LikeCount - 1);
            cp.UpdatedAt = DateTime.UtcNow;
            await _communityPlanRepository.SaveChangesAsync();
            return false; // đã unlike
        }
        else
        {
            // Like
            var like = new CommunityPlanLike
            {
                UserId          = userId,
                CommunityPlanId = communityPlanId,
                CreatedAt       = DateTime.UtcNow
            };
            await _communityPlanRepository.AddLikeAsync(like);
            cp.LikeCount++;
            cp.UpdatedAt = DateTime.UtcNow;
            await _communityPlanRepository.SaveChangesAsync();
            return true; // đã like
        }
    }

    // ── Unpublish ────────────────────────────────────────────────────────────

    public async Task UnpublishPlanAsync(Guid communityPlanId, Guid userId, bool isAdmin = false)
    {
        var cp = await _communityPlanRepository.GetByIdAsync(communityPlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy community plan.");

        if (!isAdmin && cp.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền gỡ plan này khỏi thư viện.");

        // Soft delete: đặt status = "removed" thay vì xóa hẳn để giữ lịch sử PlanCopy
        cp.Status    = "removed";
        cp.UpdatedAt = DateTime.UtcNow;
        await _communityPlanRepository.SaveChangesAsync();
    }

    public async Task<List<CommunityPlanSummaryDto>> GetMyPublishedPlansAsync(Guid userId)
    {
        var list = await _communityPlanRepository.GetByUserIdAsync(userId);
        return list.Select(cp => MapToSummaryDto(cp, null)).ToList();
    }

    // ── Admin actions ─────────────────────────────────────────────────────────

    public async Task<PagedResultDto<CommunityPlanSummaryDto>> GetPendingPlansAsync(int page, int pageSize)
    {
        var (items, totalCount) = await _communityPlanRepository.GetPendingPagedAsync(page, pageSize);

        return new PagedResultDto<CommunityPlanSummaryDto>
        {
            Items      = items.Select(cp => MapToSummaryDto(cp, null)).ToList(),
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<CommunityPlanDto> ApprovePlanAsync(Guid id, Guid adminId)
    {
        var cp = await _communityPlanRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy community plan.");

        if (cp.Status != "pending")
            throw new InvalidOperationException("Chỉ có thể duyệt plan đang ở trạng thái pending.");

        cp.Status     = "approved";
        cp.ReviewedBy = adminId;
        cp.ReviewedAt = DateTime.UtcNow;
        cp.UpdatedAt  = DateTime.UtcNow;

        await _communityPlanRepository.SaveChangesAsync();
        return MapToDto(cp, null, false);
    }

    public async Task<CommunityPlanDto> RejectPlanAsync(Guid id, Guid adminId, string reason)
    {
        var cp = await _communityPlanRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy community plan.");

        if (cp.Status != "pending")
            throw new InvalidOperationException("Chỉ có thể từ chối plan đang ở trạng thái pending.");

        cp.Status       = "rejected";
        cp.ReviewedBy   = adminId;
        cp.ReviewedAt   = DateTime.UtcNow;
        cp.RejectReason = reason;
        cp.UpdatedAt    = DateTime.UtcNow;

        await _communityPlanRepository.SaveChangesAsync();
        return MapToDto(cp, null, false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static PlanTask CloneTask(PlanTask source, Guid newPlanId, Guid? parentTaskId)
    {
        return new PlanTask
        {
            PlanId      = newPlanId,
            ParentTaskId = parentTaskId,
            Title       = source.Title,
            Description = source.Description,
            Priority    = source.Priority,
            Status      = "todo",
            StartDate   = source.StartDate,
            DueDate     = source.DueDate,
            Progress    = 0,
            OrderIndex  = source.OrderIndex,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
    }

    private static CommunityPlanSummaryDto MapToSummaryDto(CommunityPlan cp, bool? isLiked)
    {
        return new CommunityPlanSummaryDto
        {
            Id                  = cp.Id,
            PlanId              = cp.PlanId,
            UserId              = cp.UserId,
            AuthorName          = cp.Plan?.Title, // fallback; author name resolved from Identity in repo
            CategoryId          = cp.CategoryId,
            Title               = cp.Title,
            Description         = cp.Description,
            Status              = cp.Status,
            DownloadCount       = cp.DownloadCount,
            LikeCount           = cp.LikeCount,
            IsLikedByCurrentUser = isLiked,
            CreatedAt           = cp.CreatedAt
        };
    }

    private static CommunityPlanDto MapToDto(CommunityPlan cp, bool? isLiked, bool includePlan)
    {
        return new CommunityPlanDto
        {
            Id                   = cp.Id,
            PlanId               = cp.PlanId,
            UserId               = cp.UserId,
            CategoryId           = cp.CategoryId,
            Title                = cp.Title,
            Description          = cp.Description,
            Status               = cp.Status,
            ReviewedBy           = cp.ReviewedBy,
            ReviewedAt           = cp.ReviewedAt,
            RejectReason         = cp.RejectReason,
            DownloadCount        = cp.DownloadCount,
            LikeCount            = cp.LikeCount,
            IsLikedByCurrentUser = isLiked,
            CreatedAt            = cp.CreatedAt,
            UpdatedAt            = cp.UpdatedAt,
            Plan                 = includePlan && cp.Plan != null ? MapPlanToDto(cp.Plan) : null
        };
    }

    private static PlanDto MapPlanToDto(Plan plan)
    {
        return new PlanDto
        {
            Id            = plan.Id,
            UserId        = plan.UserId,
            TemplateId    = plan.TemplateId,
            FrameworkId   = plan.FrameworkId,
            CategoryId    = plan.CategoryId,
            Title         = plan.Title,
            Description   = plan.Description,
            Goal          = plan.Goal,
            Status        = plan.Status,
            IsPublic      = plan.IsPublic,
            Deadline      = plan.Deadline,
            Progress      = plan.Progress,
            IsAIGenerated = plan.IsAIGenerated,
            SortOrder     = plan.SortOrder,
            CreatedAt     = plan.CreatedAt,
            UpdatedAt     = plan.UpdatedAt,
            Tasks         = BuildTaskTree(plan.Tasks)
        };
    }

    private static List<PlanTaskDto> BuildTaskTree(ICollection<PlanTask>? tasks)
    {
        if (tasks == null || !tasks.Any()) return new List<PlanTaskDto>();

        var dtos = tasks.Select(t => new PlanTaskDto
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
        }).ToList();

        var dict      = dtos.ToDictionary(t => t.Id);
        var rootTasks = new List<PlanTaskDto>();

        foreach (var task in dtos.OrderBy(t => t.OrderIndex))
        {
            if (task.ParentTaskId.HasValue && dict.TryGetValue(task.ParentTaskId.Value, out var parent))
                parent.SubTasks.Add(task);
            else
                rootTasks.Add(task);
        }

        return rootTasks;
    }
}
