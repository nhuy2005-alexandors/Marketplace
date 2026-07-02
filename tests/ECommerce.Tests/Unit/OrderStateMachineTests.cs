using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void ChangeStatus_AllowsValidTransitions(OrderStatus from, OrderStatus to)
    {
        var order = new Order { Status = from, ShippingAddress = "x" };
        order.ChangeStatus(to);
        order.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public void ChangeStatus_RejectsInvalidTransitions(OrderStatus from, OrderStatus to)
    {
        var order = new Order { Status = from, ShippingAddress = "x" };
        var act = () => order.ChangeStatus(to);
        act.Should().Throw<InvalidOrderTransitionException>();
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_IsNoOp()
    {
        var order = new Order { Status = OrderStatus.Paid, ShippingAddress = "x" };
        order.ChangeStatus(OrderStatus.Paid);
        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanCancel_ReflectsState(OrderStatus status, bool expected)
    {
        new Order { Status = status, ShippingAddress = "x" }.CanCancel().Should().Be(expected);
    }
}

public class ProductStockTests
{
    [Fact]
    public void DecreaseStock_ReducesAvailable()
    {
        var p = new Product { Name = "x", Stock = 10, Price = 1 };
        p.DecreaseStock(3);
        p.Stock.Should().Be(7);
    }

    [Fact]
    public void DecreaseStock_OverStock_Throws()
    {
        var p = new Product { Name = "x", Stock = 2, Price = 1 };
        var act = () => p.DecreaseStock(5);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DecreaseStock_NonPositive_Throws()
    {
        var p = new Product { Name = "x", Stock = 5, Price = 1 };
        var act = () => p.DecreaseStock(0);
        act.Should().Throw<DomainException>();
    }
}
