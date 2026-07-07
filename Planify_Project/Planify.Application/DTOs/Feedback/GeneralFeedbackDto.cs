using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Feedback;

public class GeneralFeedbackDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Email hoặc tên user gửi (denormalized cho admin đọc nhanh).</summary>
    public string? UserName { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Rating { get; set; }

    /// <summary>Trạng thái xử lý: pending | reviewed | resolved</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateFeedbackStatusDto
{
    /// <summary>Trạng thái mới: pending | reviewed | resolved</summary>
    [Required]
    [RegularExpression("^(pending|reviewed|resolved)$",
        ErrorMessage = "Status phải là: pending, reviewed hoặc resolved.")]
    public string Status { get; set; } = string.Empty;
}
