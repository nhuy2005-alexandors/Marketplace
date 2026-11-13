using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=ECommerceDb_Dev;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
