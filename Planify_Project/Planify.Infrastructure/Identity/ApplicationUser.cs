using Microsoft.AspNetCore.Identity;
using System;

namespace Planify.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
}
