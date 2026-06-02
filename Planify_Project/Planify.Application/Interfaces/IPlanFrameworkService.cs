using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanFrameworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IPlanFrameworkService
{
    Task<ResponseDto<IEnumerable<PlanFrameworkDto>>> GetAllFrameworksAsync();
    Task<ResponseDto<PlanFrameworkDto>> GetFrameworkByIdAsync(Guid id);
    Task<ResponseDto<PlanFrameworkDto>> CreateFrameworkAsync(CreatePlanFrameworkDto dto, Guid adminId);
    Task<ResponseDto<PlanFrameworkDto>> UpdateFrameworkAsync(Guid id, UpdatePlanFrameworkDto dto);
    Task<ResponseDto<bool>> DeactivateFrameworkAsync(Guid id);
    Task<ResponseDto<bool>> DeleteFrameworkAsync(Guid id);
}
