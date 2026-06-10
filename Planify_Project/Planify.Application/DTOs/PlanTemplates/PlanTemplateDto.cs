using System;

namespace Planify.Application.DTOs.PlanTemplates;

public class PlanTemplateDto
{
    public Guid Id { get; set; }
    public Guid? FrameworkId { get; set; }
    public string? FrameworkName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Nội dung template dạng văn bản tự do.</summary>
    public string TemplateContent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
