using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class PlanTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? FrameworkId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateContent { get; set; } = string.Empty; // Nội dung template dạng plain text
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual PlanFramework? Framework { get; set; }
    public virtual ICollection<Plan> Plans { get; set; } = new List<Plan>();
}
