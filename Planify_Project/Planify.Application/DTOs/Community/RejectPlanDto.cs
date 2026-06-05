using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Community;

public class RejectPlanDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
