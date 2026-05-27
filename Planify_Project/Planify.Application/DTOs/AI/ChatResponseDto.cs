namespace Planify.Application.DTOs.AI;

public class ChatResponseDto
{
    /// <summary>Nội dung phản hồi của AI</summary>
    public string Reply { get; set; } = string.Empty;

    /// <summary>Model đang sử dụng</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Thời gian phản hồi (ms)</summary>
    public long ElapsedMs { get; set; }
}
