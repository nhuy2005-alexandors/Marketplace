using Microsoft.Data.SqlClient;

namespace ECommerce.Infrastructure.Persistence;

public static class DatabaseTargetGuard
{
    public static string ValidateIsolatedDevelopmentTarget(string? connectionString)
    {
        SqlConnectionStringBuilder target;
        try
        {
            target = new SqlConnectionStringBuilder(connectionString ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Invalid development database connection string.", ex);
        }

        if (target.InitialCatalog != "ECommerceDb_Dev" ||
            !target.DataSource.StartsWith(@"(localdb)\", StringComparison.OrdinalIgnoreCase) ||
            target.DataSource.Equals(@"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Development runtime and EF migrations require an isolated named LocalDB instance with database ECommerceDb_Dev. " +
                "The original MSSQLLocalDB/ECommerceDb target is forbidden. Configure appsettings.Local.json or ConnectionStrings__DefaultConnection.");
        }

        return target.ConnectionString;
    }
}
