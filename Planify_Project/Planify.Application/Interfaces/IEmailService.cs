using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}
