using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.PlanTemplates;

public class CreatePlanTemplateDto
{
    public Guid? FrameworkId { get; set; }
    public Guid? CategoryId { get; set; }

    [Required(ErrorMessage = "Tiêu đề template không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tiêu đề template tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Nội dung template không được để trống.")]
    public string TemplateContent { get; set; } = "{}";

    public bool IsActive { get; set; } = true;
}
