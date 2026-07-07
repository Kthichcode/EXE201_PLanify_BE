using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Feedback;
using Planify.Application.Interfaces;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Application.Services;

public class GeneralFeedbackService : IGeneralFeedbackService
{
    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "ai_quality", "ui_ux", "performance", "bug_report",
        "feature_request", "content", "subscription", "general"
    };

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending", "reviewed", "resolved"
    };

    private readonly IGeneralFeedbackRepository _repository;
    private readonly IUserRepository _userRepository;

    public GeneralFeedbackService(
        IGeneralFeedbackRepository repository,
        IUserRepository userRepository)
    {
        _repository      = repository;
        _userRepository  = userRepository;
    }

    public async Task<GeneralFeedbackDto> SubmitAsync(SubmitGeneralFeedbackDto dto, Guid userId)
    {
        // Validate category
        if (!ValidCategories.Contains(dto.Category))
            throw new ArgumentException(
                $"Hạng mục không hợp lệ. Chọn một trong: {string.Join(", ", ValidCategories)}.");

        var feedback = new GeneralFeedback
        {
            UserId      = userId,
            Category    = dto.Category.ToLower(),
            Title       = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Rating      = dto.Rating,
            Status      = "pending",
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        await _repository.AddAsync(feedback);
        await _repository.SaveChangesAsync();

        var user = await _userRepository.FindByIdAsync(userId);
        return MapToDto(feedback, user?.FullName ?? user?.Email);
    }

    public async Task<PagedResultDto<GeneralFeedbackDto>> GetAllAsync(
        string? category, string? status, int page, int pageSize)
    {
        var (items, total) = await _repository.GetAllPagedAsync(category, status, page, pageSize);

        var dtos = new List<GeneralFeedbackDto>();
        foreach (var fb in items)
        {
            var user = await _userRepository.FindByIdAsync(fb.UserId);
            dtos.Add(MapToDto(fb, user?.FullName ?? user?.Email));
        }

        return new PagedResultDto<GeneralFeedbackDto>
        {
            Items      = dtos,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<GeneralFeedbackDto?> GetByIdAsync(Guid id)
    {
        var fb = await _repository.GetByIdAsync(id);
        if (fb == null) return null;

        var user = await _userRepository.FindByIdAsync(fb.UserId);
        return MapToDto(fb, user?.FullName ?? user?.Email);
    }

    public async Task<GeneralFeedbackDto> UpdateStatusAsync(Guid id, string newStatus)
    {
        if (!ValidStatuses.Contains(newStatus))
            throw new ArgumentException(
                "Trạng thái không hợp lệ. Chọn: pending, reviewed hoặc resolved.");

        var fb = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy feedback.");

        fb.Status    = newStatus.ToLower();
        fb.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync();

        var user = await _userRepository.FindByIdAsync(fb.UserId);
        return MapToDto(fb, user?.FullName ?? user?.Email);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GeneralFeedbackDto MapToDto(GeneralFeedback fb, string? userName) =>
        new()
        {
            Id          = fb.Id,
            UserId      = fb.UserId,
            UserName    = userName,
            Category    = fb.Category,
            Title       = fb.Title,
            Description = fb.Description,
            Rating      = fb.Rating,
            Status      = fb.Status,
            CreatedAt   = fb.CreatedAt,
            UpdatedAt   = fb.UpdatedAt
        };
}
