using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Subscriptions;
using Planify.Application.Interfaces;
using Planify.Domain.Interfaces;
using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionsController(
        ISubscriptionService subscriptionService,
        IPaymentService paymentService,
        IConfiguration configuration,
        ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionService = subscriptionService;
        _paymentService = paymentService;
        _configuration = configuration;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <summary>
    /// Lấy danh sách các gói subscription đang hoạt động (Public/User)
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePlans()
    {
        var response = await _subscriptionService.GetActivePlansAsync();
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Lấy thông tin gói subscription hiện tại của người dùng đang đăng nhập
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ResponseDto<UserSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(ResponseDto<UserSubscriptionDto>.Fail("Không xác định được người dùng.", 401));
        }

        var response = await _subscriptionService.GetUserSubscriptionAsync(userId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Nâng cấp hoặc mua một gói subscription mới
    /// </summary>
    [HttpPost("upgrade")]
    [ProducesResponseType(typeof(ResponseDto<UpgradeSubscriptionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpgradeSubscription([FromBody] UpgradeSubscriptionRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(ResponseDto<UpgradeSubscriptionResultDto>.Fail("Không xác định được người dùng.", 401));
        }

        var response = await _subscriptionService.UpgradeSubscriptionAsync(userId, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("sepay-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleSePayWebhook()
    {
        try
        {
            // 1. Đọc raw body của request để phục vụ tính chữ ký HMAC-SHA256
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return BadRequest(ResponseDto<bool>.Fail("Dữ liệu webhook không hợp lệ.", 400));
            }

            // 2. Xác thực Webhook Security bằng HMAC-SHA256 signature
            var signatureHeader = Request.Headers["X-SePay-Signature"].ToString();
            var timestampHeader = Request.Headers["X-SePay-Timestamp"].ToString();
            var webhookSecret = _configuration["SePay:WebhookSecret"] ?? "nihaoma";

            if (!string.IsNullOrEmpty(signatureHeader) && !string.IsNullOrEmpty(timestampHeader))
            {
                var message = $"{timestampHeader}.{rawBody}";
                var keyBytes = Encoding.UTF8.GetBytes(webhookSecret);
                var messageBytes = Encoding.UTF8.GetBytes(message);

                using var hmac = new HMACSHA256(keyBytes);
                var hashBytes = hmac.ComputeHash(messageBytes);
                var computedHash = Convert.ToHexString(hashBytes).ToLower();
                var expectedSignature = $"sha256={computedHash}";

                if (!signatureHeader.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(ResponseDto<bool>.Fail("Xác thực chữ ký webhook SePay thất bại.", 401));
                }
            }
            else
            {
                // Fallback: Xác thực bằng Authorization header cũ nếu không có signature
                var authHeader = Request.Headers["Authorization"].ToString();
                var expectedApiKey = _configuration["SePay:ApiKey"];
                if (!string.IsNullOrEmpty(expectedApiKey))
                {
                    if (string.IsNullOrEmpty(authHeader) || !authHeader.Contains(expectedApiKey))
                    {
                        return Unauthorized(ResponseDto<bool>.Fail("Xác thực webhook SePay thất bại (thiếu chữ ký và Authorization không hợp lệ).", 401));
                    }
                }
                else
                {
                    return Unauthorized(ResponseDto<bool>.Fail("Xác thực webhook SePay thất bại (thiếu thông tin xác thực).", 401));
                }
            }

            // 3. Giải mã JSON body thủ công
            var body = JsonSerializer.Deserialize<SePayWebhookDto>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (body == null)
            {
                return BadRequest(ResponseDto<bool>.Fail("Không thể phân tích dữ liệu webhook.", 400));
            }

            // 4. Trích xuất orderCode từ content chuyển khoản hoặc trường code
            long orderCode = 0;
            var memo = body.Content ?? string.Empty;
            
            // Tìm dạng PLNFY followed by optional non-digits, then digits (hỗ trợ cả dấu ngoặc đơn, khoảng trắng...)
            var match = Regex.Match(memo, @"PLNFY[^\d]*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                // Tìm dạng số đơn thuần có độ dài từ 12 chữ số trở lên
                match = Regex.Match(memo, @"\d{12,}");
            }
            
            // Thử tìm trong trường code của webhook nếu trong content chưa thấy
            if (!match.Success && !string.IsNullOrEmpty(body.Code))
            {
                match = Regex.Match(body.Code, @"PLNFY[^\d]*(\d+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    match = Regex.Match(body.Code, @"\d{12,}");
                }
            }

            if (match.Success)
            {
                var codeString = match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value)
                    ? match.Groups[1].Value
                    : match.Value;

                if (long.TryParse(codeString, out var parsedCode))
                {
                    orderCode = parsedCode;
                }
            }

            if (orderCode == 0)
            {
                return BadRequest(ResponseDto<bool>.Fail("Không trích xuất được mã đơn hàng từ nội dung chuyển khoản.", 400));
            }

            // 5. Cập nhật trạng thái thanh toán thành công trong database
            var confirmResult = await _subscriptionService.ConfirmPaymentAsync(orderCode, "PAID", body.ReferenceCode);
            if (confirmResult.StatusCode < 200 || confirmResult.StatusCode >= 300)
            {
                return BadRequest(confirmResult);
            }

            // Trả về định dạng JSON bắt buộc của SePay để xác nhận đã xử lý
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(ResponseDto<bool>.Fail($"Lỗi xử lý webhook: {ex.Message}", 400));
        }
    }

    /// <summary>
    /// Đồng bộ trạng thái thanh toán của một hóa đơn từ SePay API
    /// </summary>
    [HttpGet("check-status/{orderCode}")]
    public async Task<IActionResult> CheckPaymentStatus([FromRoute] long orderCode)
    {
        try
        {
            var (status, txnRef) = await _paymentService.GetPaymentStatusAsync(orderCode);
            
            if (status.Equals("PAID", StringComparison.OrdinalIgnoreCase))
            {
                var confirmResult = await _subscriptionService.ConfirmPaymentAsync(orderCode, "PAID", txnRef);
                if (confirmResult.StatusCode >= 200 && confirmResult.StatusCode < 300)
                {
                    return Ok(ResponseDto<string>.Success("PAID", "Thanh toán thành công."));
                }
            }
            
            // Nếu chưa tìm thấy giao dịch khớp trên SePay, kiểm tra trong DB xem đã kích hoạt chưa
            var txn = await _subscriptionRepository.GetPaymentTransactionByRefAsync(orderCode.ToString());
            if (txn != null && txn.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ResponseDto<string>.Success("PAID", "Thanh toán thành công."));
            }

            return Ok(ResponseDto<string>.Success("PENDING", "Đang chờ thanh toán."));
        }
        catch (Exception ex)
        {
            return BadRequest(ResponseDto<string>.Fail($"Lỗi khi kiểm tra trạng thái thanh toán: {ex.Message}", 400));
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết thanh toán cho trang Checkout (Mã QR, Số tiền, Nội dung...)
    /// </summary>
    [HttpGet("checkout-info/{orderCode}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCheckoutInfo([FromRoute] long orderCode)
    {
        var txn = await _subscriptionRepository.GetPaymentTransactionByRefAsync(orderCode.ToString());
        if (txn == null)
        {
            return NotFound(ResponseDto<object>.Fail("Không tìm thấy giao dịch tương ứng.", 404));
        }

        var bankName = _configuration["SePay:BankName"] ?? "MBBank";
        var bankAccount = _configuration["SePay:BankAccount"] ?? "999999999999";
        var accountName = _configuration["SePay:AccountName"] ?? "NGUYEN DANG KHOA";

        var description = $"PLNFY{orderCode}";
        var qrUrl = $"https://qr.sepay.vn/img?bank={bankName}&acc={bankAccount}&template=compact&amount={txn.Amount}&des={description}";

        return Ok(ResponseDto<object>.Success(new
        {
            OrderCode = orderCode,
            Amount = txn.Amount,
            Description = description,
            BankName = bankName,
            BankAccount = bankAccount,
            AccountName = accountName,
            QrUrl = qrUrl,
            Status = txn.Status // "pending", "success", "failed"
        }, "Lấy thông tin thanh toán thành công."));
    }
}

public class SePayWebhookDto
{
    public long Id { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public string TransactionDate { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string SubAccount { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TransferType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TransferAmount { get; set; }
    public decimal Accumulated { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
}
