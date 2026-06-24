using System.Collections.Generic;

namespace Planify.Application.DTOs.Subscriptions;

public class RevenueStatisticsDto
{
    public decimal TotalRevenue { get; set; }
    public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
    public List<YearlyRevenueDto> YearlyRevenue { get; set; } = new();
    public List<DailyRevenueDto> DailyRevenue { get; set; } = new();
}
public class DailyRevenueDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public decimal Revenue { get; set; }
}
public class MonthlyRevenueDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class YearlyRevenueDto
{
    public int Year { get; set; }
    public decimal Revenue { get; set; }
}
