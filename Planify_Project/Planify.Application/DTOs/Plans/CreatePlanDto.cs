using System;
using System.Collections.Generic;

namespace Planify.Application.DTOs.Plans;

public class CreatePlanDto
{
    public Guid? TemplateId { get; set; }
    public Guid? FrameworkId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public bool IsPublic { get; set; } = false;
    public DateTime? Deadline { get; set; }
}
