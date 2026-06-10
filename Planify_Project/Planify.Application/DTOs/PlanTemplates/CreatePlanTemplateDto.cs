using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.PlanTemplates;

public class CreatePlanTemplateDto
{
    public Guid? FrameworkId { get; set; }

    [Required(ErrorMessage = "Tiêu đề template không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tiêu đề template tối đa 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Nội dung template dưới dạng mảng các dòng văn bản.
    /// Mỗi phần tử là 1 dòng — admin copy từng dòng từ Word/internet vào đây.
    /// Server sẽ tự nối lại thành văn bản hoàn chỉnh bằng dấu xuống dòng.
    /// VD: ["Giai đoạn 1: Phân tích", "- Xác định mục tiêu", "", "Giai đoạn 2: Thiết kế"]
    /// </summary>
    [Required(ErrorMessage = "Nội dung template không được để trống.")]
    [MinLength(1, ErrorMessage = "Template phải có ít nhất 1 dòng nội dung.")]
    public List<string> TemplateContent { get; set; } = new();

    public bool IsActive { get; set; } = true;
}
