using Microsoft.AspNetCore.Identity;
using System;

namespace Planify.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Trạng thái onboarding tour: not_started | in_progress | completed | skipped</summary>
    public string OnboardingStatus { get; set; } = "not_started";

    /// <summary>Bước onboarding đang dừng lại (0 = chưa bắt đầu). Dùng để resume nếu user dừng giữa chừng.</summary>
    public int OnboardingStep { get; set; } = 0;

    /// <summary>Ngày tạo tài khoản (UTC). Tự động gán khi user đăng ký.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
