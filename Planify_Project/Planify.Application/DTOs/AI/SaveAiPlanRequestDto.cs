using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.AI;

/// <summary>
/// FE gửi lên planData (nguyên bản từ AI response) để BE parse và lưu vào DB.
/// </summary>
public class SaveAiPlanRequestDto
{
    /// <summary>
    /// Toàn bộ planData object nhận được từ /api/ai/generate-plan
    /// Gồm: plan, tasks[], metadata
    /// </summary>
    [Required]
    public JsonObject PlanData { get; set; } = new();
}
