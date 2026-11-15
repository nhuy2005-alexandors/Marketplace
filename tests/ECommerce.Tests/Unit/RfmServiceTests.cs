using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECommerce.Tests.Unit;

public sealed class RfmServiceTests
{
    private static readonly DateTime AsOfUtc = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetCustomersAsync_AssignsNoPurchaseWithoutSyntheticRfmScoresAndCountsAllSegments()
    {
        using var db = CreateDb();
        db.Users.AddRange(Customer(1), Customer(2));
        db.Orders.Add(EligibleOrder(21, 2, AsOfUtc.AddDays(-3), 45m));
        await db.SaveChangesAsync();

        var page = await new RfmService(db).GetCustomersAsync(1, 25, AsOfUtc);

        page.TotalCustomers.Should().Be(2);
        var noPurchase = page.Items.Single(row => row.UserId == 1);
        noPurchase.SegmentCode.Should().Be("NoPurchase");
        noPurchase.RecencyDays.Should().BeNull();
        noPurchase.Frequency.Should().Be(0);
        noPurchase.MonetaryUsd.Should().Be(0m);
        noPurchase.RQuintile.Should().BeNull();
        noPurchase.FQuintile.Should().BeNull();
        noPurchase.MQuintile.Should().BeNull();
        page.SegmentCounts.Should().Contain(row => row.SegmentCode == "NoPurchase" && row.Count == 1);
        page.AsOfUtc.Should().Be(AsOfUtc);
    }

    [Fact]
    public async Task GetCustomersAsync_UsesEligibleOrdersAndCountsPaymentAmountOncePerDistinctOrder()
    {
        using var db = CreateDb();
        db.Users.Add(Customer(3));
        db.Orders.AddRange(
            EligibleOrder(31, 3, AsOfUtc.AddDays(-5), 70m),
            EligibleOrder(32, 3, AsOfUtc.AddDays(-2), 30m),
            EligibleOrder(33, 3, AsOfUtc.AddDays(-1), 500m, OrderStatus.Pending, PaymentStatus.Completed),
            EligibleOrder(34, 3, AsOfUtc.AddDays(-1), 400m, OrderStatus.Delivered, PaymentStatus.Failed),
            EligibleOrder(35, 3, AsOfUtc, 300m));
        await db.SaveChangesAsync();

        var page = await new RfmService(db).GetCustomersAsync(1, 25, AsOfUtc);
        var customer = page.Items.Single();

        customer.Frequency.Should().Be(2);
        customer.MonetaryUsd.Should().Be(100m);
        customer.LastEligibleOrderCreatedAtUtc.Should().Be(AsOfUtc.AddDays(-2));
        customer.RecencyDays.Should().Be(2);
        customer.RQuintile.Should().Be(5);
        customer.FQuintile.Should().Be(5);
        customer.MQuintile.Should().Be(5);
    }

    [Fact]
    public async Task GetCustomersAsync_DoesNotSplitEqualMetricsAcrossQuintiles()
    {
        using var db = CreateDb();
        db.Users.AddRange(Enumerable.Range(10, 5).Select(Customer));
        db.Orders.AddRange(Enumerable.Range(10, 5)
            .Select(userId => EligibleOrder(userId + 100, userId, AsOfUtc.AddDays(-1), 100m)));
        await db.SaveChangesAsync();

        var page = await new RfmService(db).GetCustomersAsync(1, 25, AsOfUtc);

        page.Items.Should().HaveCount(5);
        page.Items.Select(row => row.RQuintile).Distinct().Should().ContainSingle();
        page.Items.Select(row => row.FQuintile).Distinct().Should().ContainSingle();
        page.Items.Select(row => row.MQuintile).Distinct().Should().ContainSingle();
        page.Items.Select(row => row.SegmentCode).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task GetCustomersAsync_IsReadOnlyAndPaginatesWithGlobalSegmentCounts()
    {
        var writes = new SaveChangesCounter();
        using var db = CreateDb(writes);
        db.Users.AddRange(Customer(40), Customer(41), Customer(42));
        db.Orders.AddRange(
            EligibleOrder(140, 40, AsOfUtc.AddDays(-3), 25m),
            EligibleOrder(141, 41, AsOfUtc.AddDays(-2), 35m));
        await db.SaveChangesAsync();
        writes.Reset();
        var countsBefore = await db.Users.CountAsync();

        var page = await new RfmService(db).GetCustomersAsync(2, 1, AsOfUtc);

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.TotalCustomers.Should().Be(3);
        page.Items.Should().ContainSingle();
        page.SegmentCounts.Sum(segment => segment.Count).Should().Be(3);
        writes.CallCount.Should().Be(0);
        (await db.Users.CountAsync()).Should().Be(countsBefore);
        db.ChangeTracker.Entries().Should().OnlyContain(entry => entry.State == EntityState.Unchanged);

        var beyondRange = await new RfmService(db).GetCustomersAsync(int.MaxValue, 1, AsOfUtc);
        beyondRange.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, null, null, "NoPurchase")]
    [InlineData(5, 5, 5, "Champions")]
    [InlineData(1, 4, 5, "AtRisk")]
    [InlineData(3, 5, 1, "Loyal")]
    [InlineData(5, 2, 1, "PotentialLoyalist")]
    [InlineData(3, 2, 5, "HighValue")]
    [InlineData(2, 2, 2, "NeedsAttention")]
    public void MapSegment_ReturnsOneDocumentedSegment(int? r, int? f, int? m, string expected)
    {
        RfmService.MapSegment(r, f, m).Code.Should().Be(expected);
    }

    private static AppDbContext CreateDb(SaveChangesCounter? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rfm-{Guid.NewGuid()}");
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return new AppDbContext(builder.Options);
    }

    private static User Customer(int id) => new()
    {
        Id = id,
        Email = $"customer-{id}@example.test",
        PasswordHash = "test-only",
        FullName = $"Customer {id}",
        Role = UserRole.Customer,
        CreatedAt = AsOfUtc.AddDays(-30)
    };

    private static Order EligibleOrder(
        int id,
        int userId,
        DateTime createdAt,
        decimal paymentAmount,
        OrderStatus orderStatus = OrderStatus.Delivered,
        PaymentStatus paymentStatus = PaymentStatus.Completed) => new()
    {
        Id = id,
        UserId = userId,
        Status = orderStatus,
        ShippingAddress = "test-only",
        CreatedAt = createdAt,
        Payment = new Payment
        {
            Id = id,
            OrderId = id,
            Amount = paymentAmount,
            Status = paymentStatus,
            CreatedAt = createdAt
        }
    };

    private sealed class SaveChangesCounter : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public void Reset() => CallCount = 0;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            CallCount++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
