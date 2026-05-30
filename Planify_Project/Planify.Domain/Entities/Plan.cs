using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? FrameworkId { get; set; }
    public Guid? CategoryId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string Status { get; set; } = "active";
    public bool IsPublic { get; set; } = false;
    public DateTime? Deadline { get; set; }
    public int Progress { get; set; } = 0;
    public bool IsAIGenerated { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    /// <summary>Thời điểm hết hạn của bản draft. Null nếu plan đã active.</summary>
    public DateTime? DraftExpiresAt { get; set; }

    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual ICollection<PlanTask> Tasks { get; set; } = new List<PlanTask>();
}
