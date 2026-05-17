using Microsoft.AspNetCore.Identity;

namespace Planify.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
