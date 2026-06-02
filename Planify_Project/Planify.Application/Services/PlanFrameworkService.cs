using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanFrameworks;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Application.Services;

public class PlanFrameworkService : IPlanFrameworkService
{
    private readonly IPlanFrameworkRepository _repo;

    public PlanFrameworkService(IPlanFrameworkRepository repo)
    {
        _repo = repo;
    }

    public async Task<ResponseDto<IEnumerable<PlanFrameworkDto>>> GetAllFrameworksAsync()
    {
        var frameworks = await _repo.GetAllAsync();
        return ResponseDto<IEnumerable<PlanFrameworkDto>>.Success(
            frameworks.Select(MapToDto),
            "Lấy danh sách framework thành công.");
    }

    public async Task<ResponseDto<PlanFrameworkDto>> GetFrameworkByIdAsync(Guid id)
    {
        var framework = await _repo.GetByIdAsync(id);
        if (framework == null)
            return ResponseDto<PlanFrameworkDto>.Fail("Không tìm thấy framework.", 404);

        return ResponseDto<PlanFrameworkDto>.Success(MapToDto(framework), "Lấy thông tin framework thành công.");
    }

    public async Task<ResponseDto<PlanFrameworkDto>> CreateFrameworkAsync(CreatePlanFrameworkDto dto, Guid adminId)
    {
        if (await _repo.SlugExistsAsync(dto.Slug))
            return ResponseDto<PlanFrameworkDto>.Fail("Slug framework đã tồn tại.", 400);

        var framework = new PlanFramework
        {
            Name = dto.Name,
            Slug = dto.Slug.ToLower(),
            Description = dto.Description,
            Structure = dto.Structure,
            IsActive = dto.IsActive,
            CreatedBy = adminId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(framework);
        await _repo.SaveChangesAsync();

        return ResponseDto<PlanFrameworkDto>.Success(MapToDto(framework), "Tạo framework thành công.", 201);
    }

    public async Task<ResponseDto<PlanFrameworkDto>> UpdateFrameworkAsync(Guid id, UpdatePlanFrameworkDto dto)
    {
        var framework = await _repo.GetByIdAsync(id);
        if (framework == null)
            return ResponseDto<PlanFrameworkDto>.Fail("Không tìm thấy framework cần chỉnh sửa.", 404);

        if (await _repo.SlugExistsAsync(dto.Slug, id))
            return ResponseDto<PlanFrameworkDto>.Fail("Slug framework đã tồn tại ở framework khác.", 400);

        framework.Name = dto.Name;
        framework.Slug = dto.Slug.ToLower();
        framework.Description = dto.Description;
        framework.Structure = dto.Structure;
        framework.IsActive = dto.IsActive;
        framework.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        return ResponseDto<PlanFrameworkDto>.Success(MapToDto(framework), "Cập nhật framework thành công.");
    }

    public async Task<ResponseDto<bool>> DeactivateFrameworkAsync(Guid id)
    {
        var framework = await _repo.GetByIdAsync(id);
        if (framework == null)
            return ResponseDto<bool>.Fail("Không tìm thấy framework cần vô hiệu hóa.", 404);

        framework.IsActive = false;
        framework.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();
        return ResponseDto<bool>.Success(true, "Vô hiệu hóa framework thành công.");
    }

    public async Task<ResponseDto<bool>> DeleteFrameworkAsync(Guid id)
    {
        var framework = await _repo.GetByIdAsync(id);
        if (framework == null)
            return ResponseDto<bool>.Fail("Không tìm thấy framework cần xóa.", 404);

        await _repo.DeleteAsync(framework);
        await _repo.SaveChangesAsync();
        return ResponseDto<bool>.Success(true, "Xóa framework thành công.");
    }

    private static PlanFrameworkDto MapToDto(PlanFramework f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Slug = f.Slug,
        Description = f.Description,
        Structure = f.Structure,
        IsActive = f.IsActive,
        CreatedBy = f.CreatedBy,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };
}
