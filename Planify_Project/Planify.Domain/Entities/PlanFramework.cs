using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class PlanFramework
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Structure { get; set; } = "{}"; // JSON structure of the framework (tasks schema etc.)
    /// <summary>
    /// Danh sách từ khóa phân cách bởi dấu phẩy, dùng để AI tự detect loại kế hoạch từ prompt của user.
    /// VD: "website,web,wdlc,lập trình web,front-end,back-end"
    /// </summary>
    public string? Keywords { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<PlanTemplate> Templates { get; set; } = new List<PlanTemplate>();
    public virtual ICollection<Plan> Plans { get; set; } = new List<Plan>();
}
