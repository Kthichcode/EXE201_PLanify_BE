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

public class PlanFeedbackService : IPlanFeedbackService
{
    private readonly IPlanFeedbackRepository _feedbackRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IUserRepository _userRepository;

    public PlanFeedbackService(
        IPlanFeedbackRepository feedbackRepository,
        IPlanRepository planRepository,
        IUserRepository userRepository)
    {
        _feedbackRepository = feedbackRepository;
        _planRepository     = planRepository;
        _userRepository     = userRepository;
    }

    public async Task<PlanFeedbackDto> SubmitFeedbackAsync(SubmitFeedbackDto dto, Guid userId)
    {
        // 1. Validate plan tồn tại và thuộc về user
        var plan = await _planRepository.GetByIdAsync(dto.PlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy kế hoạch.");

        if (plan.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền gửi feedback cho plan này.");

        // 2. Kiểm tra xem plan có phải AI-generated không (chỉ AI plan mới cần survey)
        if (!plan.IsAIGenerated)
            throw new InvalidOperationException("Chỉ có thể gửi khảo sát cho kế hoạch được tạo bởi AI.");

        // 3. Mỗi user chỉ được gửi 1 feedback cho mỗi plan
        var alreadySubmitted = await _feedbackRepository.ExistsByUserAndPlanAsync(userId, dto.PlanId);
        if (alreadySubmitted)
            throw new InvalidOperationException("Bạn đã gửi phản hồi cho kế hoạch này rồi.");

        // 4. Validate: nếu không hiệu quả thì phải có lý do
        if (!dto.IsEffective && string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Vui lòng cung cấp lý do khi đánh giá plan không hiệu quả.");

        // 5. Tạo feedback
        var feedback = new PlanFeedback
        {
            PlanId          = dto.PlanId,
            UserId          = userId,
            CommunityPlanId = dto.CommunityPlanId,
            IsEffective     = dto.IsEffective,
            Reason          = dto.Reason?.Trim(),
            Suggestions     = dto.Suggestions?.Trim(),
            Rating          = dto.Rating,
            CreatedAt       = DateTime.UtcNow
        };

        await _feedbackRepository.AddAsync(feedback);
        await _feedbackRepository.SaveChangesAsync();

        // 6. Lấy thông tin user để trả về DTO
        var user = await _userRepository.FindByIdAsync(userId);
        return MapToDto(feedback, plan.Title, user?.FullName ?? user?.Email ?? string.Empty);
    }

    public async Task<List<PlanFeedbackDto>> GetFeedbackByPlanAsync(Guid planId)
    {
        var feedbacks = await _feedbackRepository.GetByPlanIdAsync(planId);

        var result = new List<PlanFeedbackDto>();
        foreach (var fb in feedbacks)
        {
            var plan = await _planRepository.GetByIdAsync(fb.PlanId);
            var user = await _userRepository.FindByIdAsync(fb.UserId);
            result.Add(MapToDto(fb, plan?.Title, user?.FullName ?? user?.Email ?? string.Empty));
        }
        return result;
    }

    public async Task<PagedResultDto<PlanFeedbackDto>> GetAllFeedbackAsync(int page, int pageSize)
    {
        var (feedbacks, total) = await _feedbackRepository.GetAllPagedAsync(page, pageSize);

        var items = new List<PlanFeedbackDto>();
        foreach (var fb in feedbacks)
        {
            var plan = await _planRepository.GetByIdAsync(fb.PlanId);
            var user = await _userRepository.FindByIdAsync(fb.UserId);
            items.Add(MapToDto(fb, plan?.Title, user?.FullName ?? user?.Email ?? string.Empty));
        }

        return new PagedResultDto<PlanFeedbackDto>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static PlanFeedbackDto MapToDto(PlanFeedback fb, string? planTitle, string? userName)
    {
        return new PlanFeedbackDto
        {
            Id              = fb.Id,
            PlanId          = fb.PlanId,
            UserId          = fb.UserId,
            CommunityPlanId = fb.CommunityPlanId,
            IsEffective     = fb.IsEffective,
            Reason          = fb.Reason,
            Suggestions     = fb.Suggestions,
            Rating          = fb.Rating,
            PlanTitle       = planTitle,
            UserName        = userName,
            CreatedAt       = fb.CreatedAt
        };
    }
}
