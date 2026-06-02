using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanTemplates;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Application.Services;

public class PlanTemplateService : IPlanTemplateService
{
    private readonly IPlanTemplateRepository _repo;
    private readonly IPlanFrameworkRepository _frameworkRepo;

    public PlanTemplateService(IPlanTemplateRepository repo, IPlanFrameworkRepository frameworkRepo)
    {
        _repo = repo;
        _frameworkRepo = frameworkRepo;
    }

    public async Task<ResponseDto<IEnumerable<PlanTemplateDto>>> GetAllTemplatesAsync()
    {
        var templates = await _repo.GetAllAsync();
        return ResponseDto<IEnumerable<PlanTemplateDto>>.Success(
            templates.Select(MapToDto),
            "Lấy danh sách template thành công.");
    }

    public async Task<ResponseDto<PlanTemplateDto>> GetTemplateByIdAsync(Guid id)
    {
        var template = await _repo.GetByIdAsync(id);
        if (template == null)
            return ResponseDto<PlanTemplateDto>.Fail("Không tìm thấy template.", 404);

        return ResponseDto<PlanTemplateDto>.Success(MapToDto(template), "Lấy thông tin template thành công.");
    }

    public async Task<ResponseDto<IEnumerable<PlanTemplateDto>>> GetTemplatesByFrameworkIdAsync(Guid frameworkId)
    {
        var templates = await _repo.GetByFrameworkIdAsync(frameworkId);
        return ResponseDto<IEnumerable<PlanTemplateDto>>.Success(
            templates.Select(MapToDto),
            "Lấy danh sách template theo framework thành công.");
    }

    public async Task<ResponseDto<PlanTemplateDto>> CreateTemplateAsync(CreatePlanTemplateDto dto, Guid adminId)
    {
        if (dto.FrameworkId.HasValue)
        {
            var framework = await _frameworkRepo.GetByIdAsync(dto.FrameworkId.Value);
            if (framework == null)
                return ResponseDto<PlanTemplateDto>.Fail("Không tìm thấy framework chỉ định.", 400);
        }

        var template = new PlanTemplate
        {
            FrameworkId = dto.FrameworkId,
            CategoryId = dto.CategoryId,
            Title = dto.Title,
            Description = dto.Description,
            TemplateContent = dto.TemplateContent,
            IsActive = dto.IsActive,
            CreatedBy = adminId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(template);
        await _repo.SaveChangesAsync();

        return ResponseDto<PlanTemplateDto>.Success(MapToDto(template), "Tạo template thành công.", 201);
    }

    public async Task<ResponseDto<PlanTemplateDto>> UpdateTemplateAsync(Guid id, UpdatePlanTemplateDto dto)
    {
        var template = await _repo.GetByIdAsync(id);
        if (template == null)
            return ResponseDto<PlanTemplateDto>.Fail("Không tìm thấy template cần chỉnh sửa.", 404);

        if (dto.FrameworkId.HasValue)
        {
            var framework = await _frameworkRepo.GetByIdAsync(dto.FrameworkId.Value);
            if (framework == null)
                return ResponseDto<PlanTemplateDto>.Fail("Không tìm thấy framework chỉ định.", 400);
        }

        template.FrameworkId = dto.FrameworkId;
        template.CategoryId = dto.CategoryId;
        template.Title = dto.Title;
        template.Description = dto.Description;
        template.TemplateContent = dto.TemplateContent;
        template.IsActive = dto.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        return ResponseDto<PlanTemplateDto>.Success(MapToDto(template), "Cập nhật template thành công.");
    }

    public async Task<ResponseDto<bool>> DeactivateTemplateAsync(Guid id)
    {
        var template = await _repo.GetByIdAsync(id);
        if (template == null)
            return ResponseDto<bool>.Fail("Không tìm thấy template cần vô hiệu hóa.", 404);

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();
        return ResponseDto<bool>.Success(true, "Vô hiệu hóa template thành công.");
    }

    public async Task<ResponseDto<bool>> DeleteTemplateAsync(Guid id)
    {
        var template = await _repo.GetByIdAsync(id);
        if (template == null)
            return ResponseDto<bool>.Fail("Không tìm thấy template cần xóa.", 404);

        await _repo.DeleteAsync(template);
        await _repo.SaveChangesAsync();
        return ResponseDto<bool>.Success(true, "Xóa template thành công.");
    }

    private static PlanTemplateDto MapToDto(PlanTemplate t) => new()
    {
        Id = t.Id,
        FrameworkId = t.FrameworkId,
        FrameworkName = t.Framework?.Name,
        CategoryId = t.CategoryId,
        Title = t.Title,
        Description = t.Description,
        TemplateContent = t.TemplateContent,
        IsActive = t.IsActive,
        CreatedBy = t.CreatedBy,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
