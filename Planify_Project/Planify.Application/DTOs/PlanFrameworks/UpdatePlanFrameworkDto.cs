using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.PlanFrameworks;

public class UpdatePlanFrameworkDto
{
    [Required(ErrorMessage = "Tên framework không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên framework tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug không được để trống.")]
    [MaxLength(100, ErrorMessage = "Slug tối đa 100 ký tự.")]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug chỉ chứa chữ thường, số và dấu gạch ngang.")]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Structure không được để trống.")]
    public string Structure { get; set; } = "{}";

    /// <summary>
    /// Từ khóa phân cách bởi dấu phẩy để AI detect loại kế hoạch.
    /// VD: "website,web,wdlc,lập trình web"
    /// </summary>
    [MaxLength(500, ErrorMessage = "Keywords tối đa 500 ký tự.")]
    public string? Keywords { get; set; }

    public bool IsActive { get; set; }
}
