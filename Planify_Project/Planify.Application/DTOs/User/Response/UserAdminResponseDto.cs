using System;
using System.Collections.Generic;

namespace Planify.Application.DTOs.User.Response;

public class UserAdminResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IEnumerable<string> Roles { get; set; } = new List<string>();

    // Subscription Info
    public string? PlanName { get; set; }
    public string? PlanStatus { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
}
