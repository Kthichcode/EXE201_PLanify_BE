using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.PlanFrameworks;

public class CreatePlanFrameworkDto
{
    [Required(ErrorMessage = "Tên framework không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên framework tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(500, ErrorMessage = "Keywords tối đa 500 ký tự.")]
    public string? Keywords { get; set; }

    public bool IsActive { get; set; } = true;
}

