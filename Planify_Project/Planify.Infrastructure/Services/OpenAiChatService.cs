using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Planify.Application.DTOs.AI;
using Planify.Application.Interfaces;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Planify.Infrastructure.Services;

/// <summary>
/// Gọi OpenAI Chat Completions API (gpt-4o-mini) để chat và tạo kế hoạch JSON.
/// Áp dụng các quy tắc tiết kiệm token tối đa.
/// </summary>
public class OpenAiChatService : IAiChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OpenAiChatService> _logger;

    // ── Token-saving rules ────────────────────────────────────────────────
    // • System prompt ngắn gọn, không ví dụ dài dòng
    // • max_completion_tokens giới hạn rõ ràng
    // • temperature thấp → ít "sáng tạo" không cần thiết → ít token thừa
    // • Lịch sử chat chỉ giữ tối đa 6 lượt gần nhất

    // ── System prompt CHAT (ngắn gọn, ~120 token) ────────────────────────
    private const string ChatSystemPrompt =
        """
        Bạn là Planify AI - trợ lý lập kế hoạch tiếng Việt.
        Quy tắc:
        1. Chỉ trả lời bằng Tiếng Việt.
        2. Chỉ hỗ trợ chủ đề: tạo/chỉnh sửa kế hoạch, quản lý task, lập lịch, mục tiêu.
        3. Từ chối lịch sự nếu off-topic, hướng về chủ đề kế hoạch.
        4. Trả lời ngắn gọn, đúng trọng tâm, không dài dòng.
        5. Khi người dùng muốn TẠO kế hoạch → hướng dẫn dùng tính năng "Tạo kế hoạch AI".
        6. Khi người dùng muốn CHỈNH SỬA → hỏi cụ thể và đưa gợi ý.
        """;

    // ── System prompt GENERATE PLAN (AI tự parse deadline từ văn bản tự do) ──
    private const string GeneratePlanSystemPrompt =
        """
        Bạn là Planify AI. Nhiệm vụ: phân tích yêu cầu tự do của người dùng và xuất ra kế hoạch JSON chi tiết.

        QUY TẮC BẮT BUỘC:
        1. CHỈ trả về JSON thuần - không giải thích, không markdown, không backtick.
        2. Ngôn ngữ trong JSON: Tiếng Việt.
        3. Tạo ĐỦ số tasks cần thiết để bảo đảm kế hoạch toàn diện. Mỗi task phải có đủ subtasks để mô tả chi tiết các công việc cụ thể (không giới hạn số lượng, ưu tiên đầy đủ và chi tiết).
        4. Description của Plan/Task/Subtask KHÔNG ĐỂ TRỐNG.
        5. Title của Subtask phải là động từ hành động cụ thể.
        6. Priority: "low"|"medium"|"high"|"critical". Status: "todo". Progress: 0.
        7. OrderIndex bắt đầu từ 1, tăng dần trong cùng cấp.
        8. Deadline: tự suy luận từ yêu cầu ("đến hết tháng 8", "3 tháng tới", "trước Tết", v.v.). Nếu không rõ → ngày hiện tại + 90 ngày.
        9. DueDate subtask nằm trong [StartDate, DueDate] của task cha.
        10. DueDate task nằm trong [StartDate kế hoạch, Deadline].
        11. totalTasks và totalSubtasks phải đếm chính xác.
        12. NẾU có TEMPLATE THAM KHẢO: PHẢI giữ NGUYÊN XI title của MỌI task và subtask trong template - KHÔNG được bỏ bớt, gộp, hay đổi tên. Số lượng task phải BẰNG số task trong template.
        13. Nếu subtask trong template quá ít (dưới 3) hoặc nội dung quá mơ hồ → BẮT BUỘC bổ sung thêm nhiều subtask liên quan nhất có thể, TỐI THIỂU 3 subtask cho mỗi task (không giới hạn tối đa, càng chi tiết càng tốt).
        14. TOKEN BUDGET - CỰC KỲ QUAN TRỌNG: Toàn bộ JSON output PHẢI hoàn chỉnh và đóng ngoặc đầy đủ trong giới hạn 4000 tokens. Nếu kế hoạch lớn, hãy RÚT NGẮN Description (tối đa 1-2 câu mỗi field) thay vì để JSON bị cắt giữa chừng. TUYỆT ĐỐI không được để JSON ở trạng thái chưa đóng ngoặc.

        SCHEMA JSON (không thêm bớt field):
        {"plan":{"Title":"","Description":"","Goal":"","Deadline":"YYYY-MM-DD","IsAIGenerated":true,"Status":"active","Progress":0,"IsPublic":false},"tasks":[{"Title":"","Description":"","Priority":"high","Status":"todo","StartDate":"YYYY-MM-DD","DueDate":"YYYY-MM-DD","Progress":0,"OrderIndex":1,"subtasks":[{"Title":"","Description":"","Priority":"medium","Status":"todo","StartDate":"YYYY-MM-DD","DueDate":"YYYY-MM-DD","Progress":0,"OrderIndex":1}]}],"metadata":{"estimatedDays":0,"totalTasks":0,"totalSubtasks":0,"suggestedFramework":null,"message":""}}
        """;

    // ── System prompt REFINE PLAN (AI chỉnh sửa kế hoạch hiện tại theo yêu cầu) ──
    private const string RefinePlanSystemPrompt =
        """
        Bạn là Planify AI. Nhiệm vụ: nhận kế hoạch JSON hiện tại và chỉnh sửa theo yêu cầu của người dùng.

        QUY TẮC BẮT BUỘC:
        1. CHỈ trả về JSON thuần - không giải thích, không markdown, không backtick.
        2. Giữ nguyên schema JSON gốc, chỉ thay đổi nội dung theo yêu cầu.
        3. Ngôn ngữ trong JSON: Tiếng Việt.
        4. Tạo ĐỦ số tasks và subtasks cần thiết để kế hoạch toàn diện, sau khi chỉnh sửa (không giới hạn số lượng, ưu tiên đầy đủ và chi tiết).
        5. Description của Plan/Task/Subtask KHÔNG ĐỂ TRỐNG.
        6. Priority: "low"|"medium"|"high"|"critical". Status: "todo". Progress: 0.
        7. OrderIndex bắt đầu từ 1, tăng dần trong cùng cấp.
        8. DueDate subtask nằm trong [StartDate, DueDate] của task cha.
        9. DueDate task nằm trong [StartDate kế hoạch, Deadline].
        10. totalTasks và totalSubtasks phải đếm lại chính xác sau khi chỉnh sửa.
        11. metadata.message phải mô tả ngắn gọn những gì đã được chỉnh sửa.
        12. TOKEN BUDGET - CỰC KỲ QUAN TRỌNG: Toàn bộ JSON output PHẢI hoàn chỉnh và đóng ngoặc đầy đủ trong giới hạn 6000 tokens. Nếu kế hoạch lớn, hãy RÚT NGẮN Description (tối đa 1-2 câu mỗi field) thay vì để JSON bị cắt giữa chừng. TUYỆT ĐỐI không được để JSON ở trạng thái chưa đóng ngoặc.

        SCHEMA JSON (không thêm bớt field):
        {"plan":{"Title":"","Description":"","Goal":"","Deadline":"YYYY-MM-DD","IsAIGenerated":true,"Status":"active","Progress":0,"IsPublic":false},"tasks":[{"Title":"","Description":"","Priority":"high","Status":"todo","StartDate":"YYYY-MM-DD","DueDate":"YYYY-MM-DD","Progress":0,"OrderIndex":1,"subtasks":[{"Title":"","Description":"","Priority":"medium","Status":"todo","StartDate":"YYYY-MM-DD","DueDate":"YYYY-MM-DD","Progress":0,"OrderIndex":1}]}],"metadata":{"estimatedDays":0,"totalTasks":0,"totalSubtasks":0,"suggestedFramework":null,"message":""}}
        """;


    public OpenAiChatService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAiChatService> logger)
    {
        _httpClient = httpClient;
        _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        _logger = logger;

        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey chưa được cấu hình.");

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    // ── CHAT ─────────────────────────────────────────────────────────────

    public async Task<ChatResponseDto> ChatAsync(
        ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = ChatSystemPrompt }
        };

        // Giữ tối đa 6 lượt hội thoại gần nhất để tiết kiệm token
        if (request.History is { Count: > 0 })
        {
            var trimmedHistory = request.History.Count > 6
                ? request.History.TakeLast(6).ToList()
                : request.History;

            foreach (var h in trimmedHistory)
                messages.Add(new OpenAiMessage { Role = h.Role, Content = h.Content });
        }

        messages.Add(new OpenAiMessage { Role = "user", Content = request.Message });

        var reply = await CallOpenAiAsync(
            messages,
            maxTokens: 600,
            cancellationToken);

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
        string? templateContext = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var today = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");

        // Build dynamic system prompt — inject template nếu có
        var systemPrompt = BuildGeneratePlanPrompt(templateContext);

        // Chỉ 1 field — AI tự hiểu deadline/context từ văn bản tự do
        var userMessage = $"Ngày hôm nay: {today}\nYêu cầu: {request.Prompt}";

        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user",   Content = userMessage }
        };

        _logger.LogInformation(
            "Generating plan via OpenAI: prompt={Prompt}, hasTemplate={HasTemplate}",
            request.Prompt, templateContext != null);

        var reply = await CallOpenAiAsync(
            messages,
            maxTokens: 4000,
            cancellationToken);

        sw.Stop();
        _logger.LogDebug("OpenAI raw response ({ElapsedMs}ms):\n{Raw}", sw.ElapsedMilliseconds, reply.Content);

        var jsonContent = ExtractJson(reply.Content);
        JsonObject planData;
        try
        {
            var node = JsonNode.Parse(jsonContent);
            if (node is not JsonObject obj)
            {
                _logger.LogError("AI trả về JSON không phải object. Raw:\n{Raw}", reply.Content);
                throw new InvalidOperationException("AI không trả về JSON object hợp lệ. Vui lòng thử lại.");
            }
            planData = obj;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse thất bại. Raw AI response:\n{Raw}", reply.Content);
            throw new InvalidOperationException(
                $"AI không trả về JSON hợp lệ. Chi tiết: {ex.Message}. Vui lòng thử lại.", ex);
        }

        var message = planData["metadata"]?["message"]?.GetValue<string>()
            ?? "Kế hoạch đã được tạo thành công!";

        return new GeneratePlanResponseDto
        {
            PlanData  = planData,
            Message   = message,
            Model     = reply.Model,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Build system prompt cho GeneratePlan. Nếu có template, inject thêm phần tham khảo.
    /// </summary>
    private static string BuildGeneratePlanPrompt(string? templateContext)
    {
        if (string.IsNullOrWhiteSpace(templateContext))
            return GeneratePlanSystemPrompt;

        return GeneratePlanSystemPrompt + $"""


TEMPLATE BẮT BUỘC TUÂN THEO:
- PHẢI tạo đúng số lượng task bằng với số bước/task trong template dưới đây.
- PHẢI sao chép NGUYÊN XI title của từng task và subtask từ template (không được bỏ bớt, gộp, đổi tên, hay tự sáng tạo tên khác).
- Nếu subtask trong template quá ít (dưới 3) hoặc nội dung quá mơ hồ → BẮT BUỘC bổ sung thêm nhiều subtask liên quan nhất có thể, TỐI THIỂU 3 subtask mỗi task (không giới hạn tối đa, càng chi tiết càng tốt).
- Chỉnh sửa StartDate, DueDate, Description, Priority cho phù hợp với yêu cầu người dùng — KHÔNG thay đổi Title của task/subtask có sẵn trong template.

NỘI DUNG TEMPLATE:
{templateContext}
""";
    }

    // ── REFINE PLAN ───────────────────────────────────────────────────────

    public async Task<GeneratePlanResponseDto> RefinePlanAsync(
        string currentPlanJson,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var today = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");

        var userMessage =
            $"""
            Ngày hôm nay: {today}
            Kế hoạch hiện tại (JSON):
            {currentPlanJson}

            Yêu cầu chỉnh sửa: {instruction}
            """;

        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = RefinePlanSystemPrompt },
            new() { Role = "user",   Content = userMessage }
        };

        _logger.LogInformation("Refining plan via OpenAI: instruction={Instruction}", instruction);

        var reply = await CallOpenAiAsync(
            messages,
            maxTokens: 6000,
            cancellationToken);

        sw.Stop();
        _logger.LogDebug("OpenAI refine response ({ElapsedMs}ms):\n{Raw}", sw.ElapsedMilliseconds, reply.Content);

        var jsonContent = ExtractJson(reply.Content);
        JsonObject planData;
        try
        {
            var node = JsonNode.Parse(jsonContent);
            if (node is not JsonObject obj)
            {
                _logger.LogError("AI trả về JSON không phải object khi refine. Raw:\n{Raw}", reply.Content);
                throw new InvalidOperationException("AI không trả về JSON object hợp lệ. Vui lòng thử lại.");
            }
            planData = obj;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse thất bại khi refine. Raw AI response:\n{Raw}", reply.Content);
            throw new InvalidOperationException(
                $"AI không trả về JSON hợp lệ. Chi tiết: {ex.Message}. Vui lòng thử lại.", ex);
        }

        var message = planData["metadata"]?["message"]?.GetValue<string>()
            ?? "Kế hoạch đã được chỉnh sửa thành công!";

        return new GeneratePlanResponseDto
        {
            PlanData  = planData,
            Message   = message,
            Model     = reply.Model,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }



    private async Task<(string Content, string Model)> CallOpenAiAsync(
        List<OpenAiMessage> messages,
        int maxTokens,
        CancellationToken requestToken)
    {
        var payload = new OpenAiChatRequest
        {
            Model               = _model,
            Messages            = messages,
            MaxCompletionTokens = maxTokens,
            Temperature         = 0.3f,
            TopP                = 0.9f
        };

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
            requestToken, timeoutCts.Token);

        try
        {
            _logger.LogDebug("Calling OpenAI model={Model}", _model);

            var response = await _httpClient.PostAsJsonAsync(
                "/v1/chat/completions", payload, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogError("OpenAI trả về lỗi {Status}: {Body}", response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"OpenAI API lỗi {(int)response.StatusCode}: {errorBody}");
            }

            var result = await response.Content
                .ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken: linkedCts.Token);

            if (result is null)
                throw new InvalidOperationException("OpenAI trả về response rỗng.");

            var choice = result.Choices?.FirstOrDefault()
                ?? throw new InvalidOperationException("OpenAI không có choices trong response.");

            _logger.LogInformation(
                "OpenAI usage: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}",
                result.Usage?.PromptTokens, result.Usage?.CompletionTokens, result.Usage?.TotalTokens);

            return (choice.Message?.Content ?? string.Empty, result.Model ?? _model);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new InvalidOperationException("OpenAI không phản hồi sau 120 giây. Vui lòng thử lại.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối OpenAI");
            throw new InvalidOperationException(
                "Không thể kết nối tới OpenAI API. Kiểm tra kết nối mạng.", ex);
        }
    }

    private static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence    = trimmed.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        var start = trimmed.IndexOf('{');
        var end   = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return trimmed;
    }

    // ── OpenAI internal DTOs ──────────────────────────────────────────────

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")]                   public string Model               { get; set; } = string.Empty;
        [JsonPropertyName("messages")]                public List<OpenAiMessage> Messages { get; set; } = new();
        [JsonPropertyName("max_completion_tokens")]   public int MaxCompletionTokens    { get; set; }
        [JsonPropertyName("temperature")]             public float Temperature           { get; set; }
        [JsonPropertyName("top_p")]                   public float TopP                 { get; set; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("role")]    public string Role    { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("model")]   public string?             Model   { get; set; }
        [JsonPropertyName("choices")] public List<OpenAiChoice>? Choices { get; set; }
        [JsonPropertyName("usage")]   public OpenAiUsage?        Usage   { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")] public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")]     public int PromptTokens     { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")]      public int TotalTokens      { get; set; }
    }
}
