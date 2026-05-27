using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.AI;

public class GeneratePlanRequestDto
{
    /// <summary>Mục tiêu cần đạt được. VD: "Học IELTS đạt 6.5"</summary>
    [Required(ErrorMessage = "Vui lòng nhập mục tiêu (goal).")]
    public string Goal { get; set; } = string.Empty;

    /// <summary>Deadline của kế hoạch. Định dạng: YYYY-MM-DD</summary>
    [Required(ErrorMessage = "Vui lòng nhập deadline.")]
    public string Deadline { get; set; } = string.Empty;

    /// <summary>Mô tả thêm của người dùng (optional)</summary>
    public string? Description { get; set; }
}
