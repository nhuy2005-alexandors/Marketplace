using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public class DesignTimeConfigurationTests
{
    [Fact]
    public void Explicit_target_is_used_without_opening_database()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext(["--connection", @"Server=(localdb)\MarketplaceRefundDev20261113;Database=ECommerceDb_Dev;Integrated Security=true"]);
        Assert.Equal(@"(localdb)\MarketplaceRefundDev20261113", db.Database.GetDbConnection().DataSource);
    }

    [Theory]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=ECommerceDb_Dev;Integrated Security=true")]
    [InlineData(@"Server=(localdb)\MarketplaceRefundDev20261113;Database=ECommerceDb;Integrated Security=true")]
    [InlineData("")]
    public void Unsafe_or_missing_explicit_target_fails_closed(string target)
        => Assert.Throws<InvalidOperationException>(() => new DesignTimeDbContextFactory().CreateDbContext(["--connection", target]));
}
