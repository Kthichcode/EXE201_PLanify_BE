using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Subscriptions;

public class UpdateSubscriptionPlanDto
{
    [Required(ErrorMessage = "Tên gói là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên gói không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá trị phải từ 0 trở lên.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Chu kỳ thanh toán là bắt buộc.")]
    public string BillingCycle { get; set; } = "monthly";

    public int? AiRequestsLimit { get; set; }
    public int? StorageLimitMb { get; set; }
    public int? MaxPlans { get; set; }
    public string? Features { get; set; }
    public bool IsActive { get; set; }
}
