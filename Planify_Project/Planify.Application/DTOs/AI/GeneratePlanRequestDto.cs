using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.AI;

public class GeneratePlanRequestDto
{
    /// <summary>
    /// Yêu cầu tự do của người dùng. VD: "Tôi muốn học IELTS 6.5 trước tháng 8/2026, hiện tại đang ở band 5.0"
    /// Người dùng có thể ghi deadline và mô tả trực tiếp vào đây.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập yêu cầu của bạn.")]
    [MinLength(10, ErrorMessage = "Yêu cầu quá ngắn, hãy mô tả rõ hơn.")]
    public string Prompt { get; set; } = string.Empty;
}
