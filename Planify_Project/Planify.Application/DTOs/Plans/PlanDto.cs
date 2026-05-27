using System;
using System.Collections.Generic;

namespace Planify.Application.DTOs.Plans;

public class PlanDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? FrameworkId { get; set; }
    public Guid? CategoryId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateTime? Deadline { get; set; }
    public int Progress { get; set; }
    public bool IsAIGenerated { get; set; }
    public int SortOrder { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<PlanTaskDto> Tasks { get; set; } = new List<PlanTaskDto>();
}
