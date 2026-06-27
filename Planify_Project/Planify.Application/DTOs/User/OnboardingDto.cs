using System.ComponentModel.DataAnnotations;

namespace Planify.Application.DTOs.User;

/// <summary>
/// Request gửi lên khi user tương tác với onboarding tour.
/// </summary>
public class UpdateOnboardingDto
{
    /// <summary>
    /// Trạng thái mới của tour: not_started | in_progress | completed | skipped
    /// </summary>
    [Required]
    [RegularExpression("^(not_started|in_progress|completed|skipped)$",
        ErrorMessage = "Status phải là 'not_started', 'in_progress', 'completed', hoặc 'skipped'.")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Bước hiện tại user đang ở (0-based index). Dùng để resume tour nếu user dừng giữa chừng.
    /// </summary>
    [Range(0, 100)]
    public int Step { get; set; } = 0;
}

/// <summary>
/// Response trả về trạng thái onboarding của user.
/// </summary>
public class OnboardingStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int    Step   { get; set; }

    /// <summary>True nếu user chưa hoàn thành và chưa bỏ qua (cần hiển thị tour).</summary>
    public bool ShouldShowTour => Status == "not_started" || Status == "in_progress";
}
