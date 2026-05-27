using System;
using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.Subscriptions;

public class UpgradeSubscriptionRequestDto
{
    [Required(ErrorMessage = "Mã gói dịch vụ (PlanId) là bắt buộc.")]
    public Guid PlanId { get; set; }

    [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
    public string PaymentMethod { get; set; } = "Simulation";
}
