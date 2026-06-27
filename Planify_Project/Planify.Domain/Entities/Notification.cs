using System;

namespace Planify.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Loai thông báo: "deadline", "system", "payment", v.v.
    /// </summary>
    public string Type { get; set; } = "system";
    
    public bool IsRead { get; set; } = false;
    
    // Optional: Lưu lại Id của entity liên quan (VD: PlanId hoặc PlanTaskId) để khi bấm vào chuông FE biết chuyển hướng đi đâu
    public Guid? ReferenceId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
