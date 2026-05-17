using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Planify.Infrastructure.Data;

/// <summary>
/// Used by EF Core CLI tools (dotnet ef migrations add ...) at design time.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            "server=.\\SQLEXPRESS; database=PlanifyDb; uid=sa; pwd=12345; TrustServerCertificate=True;");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
