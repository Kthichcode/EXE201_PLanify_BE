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
    Task<System.Collections.Generic.List<PlanDto>> GetPlansByUserIdAsync(Guid userId);
    Task<PlanTaskDto> UpdateTaskStatusAsync(Guid planId, Guid taskId, UpdateTaskStatusDto dto, Guid userId);

    Task<PlanDto> SaveAiPlanAsDraftAsync(SaveAiPlanRequestDto dto, Guid userId);

    Task<PlanDto> ConfirmDraftPlanAsync(Guid planId, Guid userId);
    Task DiscardDraftPlanAsync(Guid planId, Guid userId);

    Task DeletePlanAsync(Guid planId, Guid userId);

    Task<PlanDto> RefreshDraftWithRefinedPlanAsync(Guid planId, SaveAiPlanRequestDto dto, Guid userId);

    Task<PlanTaskDto> UpdatePlanTaskAsync(Guid planId, Guid taskId, UpdatePlanTaskDto dto, Guid userId);
    Task DeletePlanTaskAsync(Guid planId, Guid taskId, Guid userId);
}
