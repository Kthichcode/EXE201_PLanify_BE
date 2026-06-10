using System;

namespace Planify.Application.DTOs.PlanFrameworks;

public class PlanFrameworkDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Structure { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public bool IsActive { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
