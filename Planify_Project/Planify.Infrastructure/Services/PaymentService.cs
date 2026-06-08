using Microsoft.Extensions.Configuration;
using Planify.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PaymentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public Task<string> CreatePaymentLinkAsync(long orderCode, decimal amount, string description, string returnUrl, string cancelUrl)
    {
        var bankName = _configuration["SePay:BankName"] ?? "TPBank";
        var bankAccount = _configuration["SePay:BankAccount"] ?? "11140845389";
        var transferDes = $"PLNFY{orderCode}";
        
        var qrUrl = $"https://qr.sepay.vn/img?bank={bankName}&acc={bankAccount}&template=compact&amount={amount.ToString("0")}&des={transferDes}";

        return Task.FromResult(qrUrl);
    }

    public async Task<(string Status, string TransactionId)> GetPaymentStatusAsync(long orderCode)
    {
        try
        {
            var apiKey = _configuration["SePay:ApiKey"] ?? string.Empty;
            if (string.IsNullOrEmpty(apiKey))
            {
                return ("pending", string.Empty);
            }

            // Gọi API SePay để tra cứu giao dịch có chứa mã đơn hàng
            var requestUri = $"https://my.sepay.vn/api/v1/transactions?q=PLNFY{orderCode}";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return ("pending", string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<SePayTransactionListResponse>();
            if (result?.Data == null || result.Data.Count == 0)
            {
                return ("pending", string.Empty);
            }

            // Tìm kiếm giao dịch khớp mã nội dung chuyển khoản và số tiền nhận (transferType = in)
            foreach (var tx in result.Data)
            {
                var memo = tx.Content ?? string.Empty;
                var code = tx.Code ?? string.Empty;
                
                if (tx.TransferType.Equals("in", StringComparison.OrdinalIgnoreCase) && 
                    (memo.Contains($"PLNFY{orderCode}", StringComparison.OrdinalIgnoreCase) || 
                     code.Contains($"PLNFY{orderCode}", StringComparison.OrdinalIgnoreCase)))
                {
                    // Tìm thấy giao dịch thành công khớp với hóa đơn
                    var refCode = !string.IsNullOrEmpty(tx.ReferenceCode) ? tx.ReferenceCode : tx.Id.ToString();
                    return ("PAID", refCode);
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi kết nối và trả về pending để thử lại sau
        }

        return ("pending", string.Empty);
    }
}

public class SePayTransactionListResponse
{
    [JsonPropertyName("data")]
    public List<SePayTransactionDto> Data { get; set; } = new();
}

public class SePayTransactionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("gateway")]
    public string Gateway { get; set; } = string.Empty;

    [JsonPropertyName("transactionDate")]
    public string TransactionDate { get; set; } = string.Empty;

    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("transferType")]
    public string TransferType { get; set; } = string.Empty;

    [JsonPropertyName("transferAmount")]
    public decimal TransferAmount { get; set; }

    [JsonPropertyName("referenceCode")]
    public string ReferenceCode { get; set; } = string.Empty;
}
