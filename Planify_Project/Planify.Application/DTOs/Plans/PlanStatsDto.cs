namespace Planify.Application.DTOs.Plans;

/// <summary>
/// Thống kê kế hoạch của một user.
/// </summary>
public class PlanStatsDto
{
    /// <summary>Tổng số kế hoạch đã tạo (không tính draft/discarded).</summary>
    public int TotalPlans { get; set; }

    /// <summary>Số kế hoạch đã hoàn thành (status = "completed" hoặc progress = 100).</summary>
    public int CompletedPlans { get; set; }

    /// <summary>Số kế hoạch đang thực hiện (active, chưa hoàn thành).</summary>
    public int ActivePlans { get; set; }

    /// <summary>Tỉ lệ hoàn thành (%).</summary>
    public double CompletionRate => TotalPlans == 0 ? 0 : Math.Round((double)CompletedPlans / TotalPlans * 100, 1);
}
