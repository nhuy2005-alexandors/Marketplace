using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        string? Arg(string key)
        {
            var index = Array.IndexOf(args, key);
            return index < 0 ? null : index + 1 < args.Length ? args[index + 1]
                : throw new InvalidOperationException($"Missing value for {key}.");
        }
        var root = Arg("--content-root") ?? (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "src", "ECommerce.API"))
            ? Path.Combine(Directory.GetCurrentDirectory(), "src", "ECommerce.API") : Directory.GetCurrentDirectory());
        var environment = Arg("--environment") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        if (environment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || environment.Contains('/') || environment.Contains('\\'))
            throw new InvalidOperationException("Invalid environment name.");
        var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(root))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables().Build();
        var connection = Arg("--connection") ?? config.GetConnectionString("DefaultConnection");
        var safeConnection = DatabaseTargetGuard.ValidateIsolatedDevelopmentTarget(connection);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(safeConnection)
            .Options;
        return new AppDbContext(options);
    }
}
