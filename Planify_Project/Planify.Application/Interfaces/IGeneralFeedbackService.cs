using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Feedback;
using System;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IGeneralFeedbackService
{
    /// <summary>
    /// User gửi phản hồi tổng quát — có thể gửi bất kỳ lúc nào, không giới hạn.
    /// </summary>
    Task<GeneralFeedbackDto> SubmitAsync(SubmitGeneralFeedbackDto dto, Guid userId);

    /// <summary>Admin lấy danh sách feedback có lọc theo category và status.</summary>
    Task<PagedResultDto<GeneralFeedbackDto>> GetAllAsync(
        string? category, string? status, int page, int pageSize);

    /// <summary>Admin xem chi tiết một feedback.</summary>
    Task<GeneralFeedbackDto?> GetByIdAsync(Guid id);

    /// <summary>Admin cập nhật trạng thái xử lý feedback (pending → reviewed → resolved).</summary>
    Task<GeneralFeedbackDto> UpdateStatusAsync(Guid id, string newStatus);
}
