using System;

namespace Planify.Application.DTOs.Community;

public class CommunityPlanQueryDto
{
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }

    /// <summary>newest | popular | most_downloaded</summary>
    public string? SortBy { get; set; } = "newest";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
