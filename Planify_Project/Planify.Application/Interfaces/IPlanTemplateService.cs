using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanTemplates;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IPlanTemplateService
{
    Task<ResponseDto<IEnumerable<PlanTemplateDto>>> GetAllTemplatesAsync();
    Task<ResponseDto<PlanTemplateDto>> GetTemplateByIdAsync(Guid id);
    Task<ResponseDto<IEnumerable<PlanTemplateDto>>> GetTemplatesByFrameworkIdAsync(Guid frameworkId);
    Task<ResponseDto<PlanTemplateDto>> CreateTemplateAsync(CreatePlanTemplateDto dto, Guid adminId);
    Task<ResponseDto<PlanTemplateDto>> UpdateTemplateAsync(Guid id, UpdatePlanTemplateDto dto);
    Task<ResponseDto<bool>> DeactivateTemplateAsync(Guid id);
    Task<ResponseDto<bool>> DeleteTemplateAsync(Guid id);
}
