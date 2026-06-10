using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Plans;

public class UpdatePlanTaskDto
{
    [Required(ErrorMessage = "Tiêu đề nhiệm vụ không được để trống.")]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [RegularExpression("^(high|medium|low)$", ErrorMessage = "Độ ưu tiên phải là 'high', 'medium' hoặc 'low'.")]
    public string Priority { get; set; } = "medium";
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
}
