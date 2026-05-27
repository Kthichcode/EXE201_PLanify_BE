using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Plans;

public class UpdateTaskStatusDto
{
    [Required]
    [RegularExpression("^(todo|in_progress|done|overdue)$", ErrorMessage = "Status must be 'todo', 'in_progress', 'done', or 'overdue'.")]
    public string Status { get; set; } = string.Empty;
}
