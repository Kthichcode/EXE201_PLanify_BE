using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Community;

public class PublishPlanDto
{
    /// <summary>Id của plan gốc (phải thuộc user, status = active).</summary>
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }
}
