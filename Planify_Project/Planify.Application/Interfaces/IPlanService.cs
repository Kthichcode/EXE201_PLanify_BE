using Planify.Application.DTOs.AI;
using Planify.Application.DTOs.Plans;
using System;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IPlanService
{
    Task<PlanDto> CreateManualPlanAsync(CreatePlanDto dto, Guid userId);
    Task<PlanTaskDto> AddTaskToPlanAsync(Guid planId, CreatePlanTaskDto dto, Guid userId);
    Task<PlanDto?> GetPlanByIdAsync(Guid planId, Guid userId);
    Task<PlanTaskDto> UpdateTaskStatusAsync(Guid planId, Guid taskId, UpdateTaskStatusDto dto, Guid userId);

    /// <summary>
    /// Parse planData JSON từ AI → lưu vào DB với Status = "draft".
    /// Draft tự động hết hạn sau 24 giờ (xóa bởi cleanup job).
    /// </summary>
    Task<PlanDto> SaveAiPlanAsDraftAsync(SaveAiPlanRequestDto dto, Guid userId);

    /// <summary>
    /// Xác nhận kế hoạch draft → chuyển Status = "active".
    /// </summary>
    Task<PlanDto> ConfirmDraftPlanAsync(Guid planId, Guid userId);

    /// <summary>
    /// Người dùng chủ động hủy và xóa plan draft (trước khi hết hạn).
    /// </summary>
    Task DiscardDraftPlanAsync(Guid planId, Guid userId);
}
