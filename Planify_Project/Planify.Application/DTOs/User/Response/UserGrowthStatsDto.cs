namespace Planify.Application.DTOs.User.Response;

/// <summary>
/// Th?ng kê t?ng quan tang tru?ng ngu?i dùng.
/// </summary>
public class UserGrowthStatsDto
{
    /// <summary>T?ng s? user trong h? th?ng</summary>
    public int TotalUsers { get; set; }

    /// <summary>S? user m?i trong kho?ng th?i gian du?c ch?n</summary>
    public int NewUsers { get; set; }

    /// <summary>S? user m?i trong 7 ngày g?n nh?t</summary>
    public int NewUsersLast7Days { get; set; }

    /// <summary>S? user m?i trong 30 ngày g?n nh?t</summary>
    public int NewUsersLast30Days { get; set; }

    /// <summary>T? l? tang tru?ng so v?i kho?ng tru?c (%) - có th? null n?u không d? d? li?u</summary>
    public double? GrowthRatePercent { get; set; }

    /// <summary>Chi ti?t s? dang ký theo t?ng ngày trong kho?ng th?i gian</summary>
    public List<DailyRegistrationDto> DailyRegistrations { get; set; } = new();
}

/// <summary>S? lu?ng user dang ký trong 1 ngày c? th?.</summary>
public class DailyRegistrationDto
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}
