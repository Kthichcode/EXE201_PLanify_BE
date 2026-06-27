using System;
using System.Collections.Generic;

namespace Planify.Domain.Entities;

public class PlanFramework
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<PlanTemplate> Templates { get; set; } = new List<PlanTemplate>();
    public virtual ICollection<Plan> Plans { get; set; } = new List<Plan>();
}
