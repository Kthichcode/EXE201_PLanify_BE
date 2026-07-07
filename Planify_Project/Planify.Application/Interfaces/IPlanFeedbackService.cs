using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Feedback;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IPlanFeedbackService
{
    /// <summary>
    /// User gửi khảo sát đánh giá hiệu quả plan AI sau khi publish.
    /// Mỗi user chỉ được gửi 1 feedback cho mỗi plan.
    /// </summary>
    Task<PlanFeedbackDto> SubmitFeedbackAsync(SubmitFeedbackDto dto, Guid userId);

    /// <summary>Lấy tất cả feedback của một plan cụ thể (dành cho admin).</summary>
    Task<List<PlanFeedbackDto>> GetFeedbackByPlanAsync(Guid planId);

    /// <summary>Lấy toàn bộ feedback — phân trang (dành cho admin).</summary>
    Task<PagedResultDto<PlanFeedbackDto>> GetAllFeedbackAsync(int page, int pageSize);
}
