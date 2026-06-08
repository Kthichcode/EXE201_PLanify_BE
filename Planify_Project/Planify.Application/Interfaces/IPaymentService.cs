using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IPaymentService
{
    Task<string> CreatePaymentLinkAsync(long orderCode, decimal amount, string description, string returnUrl, string cancelUrl);
    Task<(string Status, string TransactionId)> GetPaymentStatusAsync(long orderCode);
}
