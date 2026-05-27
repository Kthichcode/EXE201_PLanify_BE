using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Planify.Application.DTOs.AI;
using Planify.Application.Interfaces;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Planify.Infrastructure.Services;

/// <summary>
/// Gọi Ollama REST API để chat với model llama3.
/// Hỗ trợ 2 chế độ: chat hội thoại và tạo kế hoạch JSON chuẩn.
/// </summary>
public class OllamaAiChatService : IAiChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaAiChatService> _logger;

    // ── System prompt cho chế độ CHAT thông thường ──────────────────────
    private const string ChatSystemPrompt =
        """
        Bạn là Planify AI - trợ lý thông minh hỗ trợ người dùng lập kế hoạch và quản lý công việc.

        === QUY TẮC BẮT BUỘC ===
        1. Chỉ trả lời bằng Tiếng Việt, không dùng ngôn ngữ khác.
        2. Chỉ hỗ trợ các chủ đề liên quan đến: tạo kế hoạch, chỉnh sửa kế hoạch, quản lý task, lập lịch, mục tiêu cá nhân/công việc.
        3. Nếu người dùng hỏi chủ đề không liên quan (chính trị, giải trí, code, v.v.), hãy từ chối lịch sự và hướng họ về chủ đề kế hoạch.
        4. Trả lời ngắn gọn, thân thiện, đúng trọng tâm.
        5. Khi người dùng muốn TẠO kế hoạch, hãy hướng dẫn họ cung cấp: mục tiêu, deadline, mô tả ngắn - để dùng tính năng "Tạo kế hoạch AI".
        6. Khi người dùng muốn CHỈNH SỬA kế hoạch, hãy hỏi họ muốn thay đổi gì (deadline, task, priority, v.v.) và đưa ra gợi ý cụ thể.
        """;

    // ── System prompt cho chế độ GENERATE PLAN ──────────────────────────
    private const string GeneratePlanSystemPrompt =
        """
        Bạn là Planify AI - trợ lý tạo kế hoạch thông minh.

        Nhiệm vụ của bạn là phân tích yêu cầu của người dùng và tạo ra một kế hoạch chi tiết có cấu trúc.

        === QUY TẮC BẮT BUỘC ===
        1. Chỉ trả về JSON, không giải thích thêm, không markdown, không backtick.
        2. JSON phải đúng cấu trúc bên dưới, không thêm bớt field.
        3. Ngôn ngữ: Tiếng Việt.
        4. Mỗi task phải có ít nhất 1 subtask.
        5. DueDate của subtask phải nằm trong khoảng StartDate - DueDate của task cha.
        6. DueDate của task cha phải nằm trước hoặc bằng Deadline của Plan.
        7. Priority chỉ nhận: "low" | "medium" | "high" | "critical".
        8. Status mặc định luôn là "todo".
        9. OrderIndex bắt đầu từ 1 và tăng dần.
        10. Nếu người dùng không cung cấp đủ thông tin, hãy tự suy luận hợp lý dựa trên mục tiêu.

        === CẤU TRÚC JSON ===
        {
          "plan": {
            "Title": "string - tên kế hoạch ngắn gọn, rõ ràng",
            "Description": "string - mô tả tổng quan kế hoạch",
            "Goal": "string - mục tiêu cụ thể cần đạt được",
            "Deadline": "YYYY-MM-DD",
            "IsAIGenerated": true,
            "Status": "active",
            "Progress": 0,
            "IsPublic": false
          },
          "tasks": [
            {
              "Title": "string",
              "Description": "string",
              "Priority": "low | medium | high | critical",
              "Status": "todo",
              "StartDate": "YYYY-MM-DD",
              "DueDate": "YYYY-MM-DD",
              "Progress": 0,
              "OrderIndex": 1,
              "subtasks": [
                {
                  "Title": "string",
                  "Description": "string",
                  "Priority": "low | medium | high | critical",
                  "Status": "todo",
                  "StartDate": "YYYY-MM-DD",
                  "DueDate": "YYYY-MM-DD",
                  "Progress": 0,
                  "OrderIndex": 1
                }
              ]
            }
          ],
          "metadata": {
            "estimatedDays": 30,
            "totalTasks": 4,
            "totalSubtasks": 12,
            "suggestedFramework": "string hoặc null",
            "message": "string - 1 câu giải thích ngắn về kế hoạch vừa tạo"
          }
        }
        """;

    public OllamaAiChatService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OllamaAiChatService> logger)
    {
        _httpClient = httpClient;
        _model = configuration["Ollama:Model"] ?? "llama3";
        _logger = logger;
    }

    // ── CHAT ─────────────────────────────────────────────────────────────

    public async Task<ChatResponseDto> ChatAsync(
        ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var messages = new List<OllamaMessage>
        {
            // Inject system prompt đầu tiên
            new() { Role = "system", Content = ChatSystemPrompt }
        };

        // Thêm lịch sử hội thoại
        if (request.History is { Count: > 0 })
        {
            foreach (var h in request.History)
                messages.Add(new OllamaMessage { Role = h.Role, Content = h.Content });
        }

        // Tin nhắn hiện tại
        messages.Add(new OllamaMessage { Role = "user", Content = request.Message });

        var reply = await CallOllamaAsync(messages, cancellationToken);
        sw.Stop();

        return new ChatResponseDto
        {
            Reply = reply.Content,
            Model = reply.Model,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    // ── GENERATE PLAN ─────────────────────────────────────────────────────

    public async Task<GeneratePlanResponseDto> GeneratePlanAsync(
        GeneratePlanRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var today = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd"); // GMT+7

        // Xây dựng user message theo template
        var userMessage = string.Format(
            "=== NGÀY HÔM NAY ===\n{0}\n\n=== THÔNG TIN NGƯỜI DÙNG GỬI LÊN ===\n{{\n  \"goal\": \"{1}\",\n  \"deadline\": \"{2}\",\n  \"description\": \"{3}\"\n}}",
            today,
            request.Goal,
            request.Deadline,
            request.Description ?? "Không có mô tả thêm"
        );

        var messages = new List<OllamaMessage>
        {
            new() { Role = "system", Content = GeneratePlanSystemPrompt },
            new() { Role = "user",   Content = userMessage }
        };

        _logger.LogInformation("Generating plan: goal={Goal}, deadline={Deadline}", request.Goal, request.Deadline);

        var reply = await CallOllamaAsync(messages, cancellationToken);
        sw.Stop();

        // Log raw để debug khi AI không trả JSON đúng format
        _logger.LogDebug("Ollama raw response ({ElapsedMs}ms):\n{Raw}", sw.ElapsedMilliseconds, reply.Content);

        // Parse JSON từ AI
        var jsonContent = ExtractJson(reply.Content);
        JsonObject planData;
        try
        {
            var node = JsonNode.Parse(jsonContent);
            if (node is not JsonObject obj)
            {
                _logger.LogError("AI trả về JSON không phải object. Raw:\n{Raw}", reply.Content);
                throw new InvalidOperationException(
                    $"AI không trả về JSON object hợp lệ (trả về: {node?.GetType().Name ?? "null"}). " +
                    "Vui lòng thử lại hoặc mô tả rõ hơn yêu cầu.");
            }
            planData = obj;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse thất bại. Raw AI response:\n{Raw}", reply.Content);
            throw new InvalidOperationException(
                $"AI không trả về JSON hợp lệ. Chi tiết: {ex.Message}. Vui lòng thử lại.", ex);
        }

        // Lấy message từ metadata (nếu có)
        var message = planData["metadata"]?["message"]?.GetValue<string>() ?? "Kế hoạch đã được tạo thành công!";

        return new GeneratePlanResponseDto
        {
            PlanData = planData,
            Message  = message,
            Model    = reply.Model,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    // ── HELPER ───────────────────────────────────────────────────────────

    /// <summary>Gọi Ollama /api/chat và trả về nội dung + model</summary>
    private async Task<(string Content, string Model)> CallOllamaAsync(
        List<OllamaMessage> messages,
        CancellationToken requestToken)
    {
        var payload = new OllamaChatRequest
        {
            Model    = _model,
            Messages = messages,
            Stream   = false
        };

        // Dùng timeout riêng 290s, kết hợp với request token
        // → Ollama sẽ không bị cancel khi Swagger/Postman timeout sớm hơn
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(290));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
            requestToken, timeoutCts.Token);

        try
        {
            _logger.LogDebug("Calling Ollama {Model} at {BaseAddress}/api/chat", _model, _httpClient.BaseAddress);

            var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, linkedCts.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: linkedCts.Token);

            if (result is null)
                throw new InvalidOperationException("Ollama trả về response rỗng.");

            return (result.Message?.Content ?? string.Empty, result.Model ?? _model);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Ollama không phản hồi sau 290 giây. Model '{_model}' có thể quá nặng cho máy hiện tại. " +
                "Thử dùng 'llama3:8b' hoặc giảm độ phức tạp yêu cầu.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối Ollama tại {BaseAddress}", _httpClient.BaseAddress);
            throw new InvalidOperationException(
                $"Không thể kết nối tới Ollama tại {_httpClient.BaseAddress}. " +
                "Hãy chạy 'ollama serve' và đảm bảo model đã được pull.", ex);
        }
    }

    /// <summary>
    /// Trích xuất JSON thuần từ response AI (loại bỏ markdown fences nếu có).
    /// </summary>
    private static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();

        // Nếu AI vẫn bọc backtick dù đã dặn không làm vậy
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence    = trimmed.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        // Tìm vị trí { đầu tiên và } cuối cùng để cắt JSON ra
        var start = trimmed.IndexOf('{');
        var end   = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return trimmed;
    }

    // ── Ollama internal DTOs ──────────────────────────────────────────────

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]    public string Model    { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<OllamaMessage> Messages { get; set; } = new();
        [JsonPropertyName("stream")]   public bool Stream { get; set; } = false;
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]    public string Role    { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("model")]   public string?        Model   { get; set; }
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done")]    public bool           Done    { get; set; }
    }
}
