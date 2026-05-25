using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class PlanTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public Guid? ParentTaskId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "todo";
    public string Priority { get; set; } = "medium";
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public int Progress { get; set; } = 0;
    public int OrderIndex { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Plan? Plan { get; set; }
    public virtual PlanTask? ParentTask { get; set; }
    public virtual ICollection<PlanTask> SubTasks { get; set; } = new List<PlanTask>();
}
