using System;

namespace Planify.Application.DTOs.Plans;

public class PlanTaskDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid? ParentTaskId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public int Progress { get; set; }
    public int OrderIndex { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public List<PlanTaskDto> SubTasks { get; set; } = new List<PlanTaskDto>();
}
