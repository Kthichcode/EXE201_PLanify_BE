namespace Planify.Application.DTOs.AI;

public class ChatRequestDto
{
    /// <summary>
    /// Tin nhắn của người dùng gửi tới AI
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Lịch sử hội thoại (optional) - để AI nhớ ngữ cảnh
    /// </summary>
    public List<ChatMessageDto>? History { get; set; }
}

public class ChatMessageDto
{
    /// <summary>"user" hoặc "assistant"</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
