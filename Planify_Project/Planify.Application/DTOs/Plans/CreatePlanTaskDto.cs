using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Plans;

public class CreatePlanTaskDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [RegularExpression("^(high|medium|low)$", ErrorMessage = "Priority must be 'high', 'medium', or 'low'.")]
    public string Priority { get; set; } = "medium";
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    
    public int OrderIndex { get; set; } = 0;
}
