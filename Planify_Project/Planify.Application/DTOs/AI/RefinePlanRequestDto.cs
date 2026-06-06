using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.AI;

/// <summary>
/// Request để yêu cầu AI chỉnh sửa lại kế hoạch draft đã tạo trước đó.
/// </summary>
public class RefinePlanRequestDto
{
    /// <summary>
    /// ID của kế hoạch draft cần chỉnh sửa.
    /// </summary>
    [Required(ErrorMessage = "PlanId không được để trống.")]
    public Guid PlanId { get; set; }

    /// <summary>
    /// Yêu cầu chỉnh sửa bằng ngôn ngữ tự nhiên.
    /// Ví dụ: "Thêm 2 task về marketing", "Rút ngắn deadline xuống cuối tháng 7", "Thêm subtask kiểm thử cho mỗi task"
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập yêu cầu chỉnh sửa.")]
    [MinLength(5, ErrorMessage = "Yêu cầu chỉnh sửa quá ngắn, hãy mô tả rõ hơn.")]
    public string Instruction { get; set; } = string.Empty;
}
