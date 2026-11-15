using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Integration;

public class StartupSafetyTests
{
    [Fact]
    public async Task Api_startup_does_not_migrate_or_seed_the_configured_database()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    (x.ServiceType.FullName?.Contains("DbContextOptionsConfiguration") == true && x.ServiceType.FullName.Contains("AppDbContext"))).ToList())
                    services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection)
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
            });
        });
        using var client = factory.CreateClient();
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Products.CountAsync());
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = '__EFMigrationsHistory'";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }
}
