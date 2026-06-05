using PayOS;
using PayOS.Models.V2.PaymentRequests;
using Planify.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Planify.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly PayOSClient _payOSClient;

    public PaymentService(PayOSClient payOSClient)
    {
        _payOSClient = payOSClient;
    }

    public async Task<string> CreatePaymentLinkAsync(long orderCode, decimal amount, string description, string returnUrl, string cancelUrl)
    {
        // PayOS description limit is 25 characters, alphanumeric & space only
        var cleanDescription = description.Length > 25 ? description[..25] : description;

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)amount,
            Description = cleanDescription,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl
        };

        var response = await _payOSClient.PaymentRequests.CreateAsync(request);
        return response.CheckoutUrl;
    }

    public async Task<(string Status, string TransactionId)> GetPaymentStatusAsync(long orderCode)
    {
        var info = await _payOSClient.PaymentRequests.GetAsync(orderCode);
        return (info.Status.ToString(), info.Id);
    }
}
