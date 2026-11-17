using System.Data;
using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence;

public sealed class DashboardStoredProcedureQueries(AppDbContext db) : IDashboardStoredProcedureQueries
{
    public Task<IReadOnlyList<StatusCountDto>> GetOrderStatusSummaryAsync(
        int? sellerId,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlServer())
            return GetOrderStatusSummaryFallbackAsync(sellerId, cancellationToken);

        return ExecuteOrderStatusSummaryAsync(sellerId, cancellationToken);
    }

    public Task<IReadOnlyList<TopProductDto>> GetTopSellingProductsAsync(
        int? sellerId,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlServer())
            return GetTopSellingProductsFallbackAsync(sellerId, cancellationToken);

        return ExecuteTopSellingProductsAsync(sellerId, cancellationToken);
    }

    private async Task<IReadOnlyList<StatusCountDto>> ExecuteOrderStatusSummaryAsync(
        int? sellerId,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_GetOrderStatusSummary";
            command.CommandType = CommandType.StoredProcedure;
            AddParameter(command, "@SellerId", DbType.Int32, sellerId.HasValue ? sellerId.Value : DBNull.Value);

            var results = new List<StatusCountDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var status = (OrderStatus)reader.GetInt32(0);
                results.Add(new StatusCountDto(status.ToString(), reader.GetInt32(1)));
            }

            return results;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<TopProductDto>> ExecuteTopSellingProductsAsync(
        int? sellerId,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_GetTopSellingProducts";
            command.CommandType = CommandType.StoredProcedure;
            AddParameter(command, "@SellerId", DbType.Int32, sellerId.HasValue ? sellerId.Value : DBNull.Value);
            AddParameter(command, "@Take", DbType.Int32, 5);

            var results = new List<TopProductDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new TopProductDto(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetDecimal(3)));
            }

            return results;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private Task<IReadOnlyList<StatusCountDto>> GetOrderStatusSummaryFallbackAsync(
        int? sellerId,
        CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();
        if (sellerId.HasValue)
            query = query.Where(order => order.Items.Any(item => item.SellerId == sellerId.Value));

        return ProjectStatusCountsAsync(query, cancellationToken);
    }

    private static async Task<IReadOnlyList<StatusCountDto>> ProjectStatusCountsAsync(
        IQueryable<ECommerce.Domain.Entities.Order> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(row => row.Status)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new StatusCountDto(row.Status.ToString(), row.Count)).ToList();
    }

    private Task<IReadOnlyList<TopProductDto>> GetTopSellingProductsFallbackAsync(
        int? sellerId,
        CancellationToken cancellationToken)
    {
        var eligibleStatuses = new[] { OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered };
        var query = db.OrderItems.AsNoTracking().Where(item =>
            item.Status != FulfillmentStatus.Cancelled &&
            eligibleStatuses.Contains(item.Order.Status) &&
            item.Order.Payment != null &&
            item.Order.Payment.Status == PaymentStatus.Completed &&
            !item.Order.Payment.RequiresReconciliation &&
            !item.Order.Payment.IsDemo);

        if (sellerId.HasValue)
            query = query.Where(item => item.SellerId == sellerId.Value);

        return ProjectTopProductsAsync(query, cancellationToken);
    }

    private static async Task<IReadOnlyList<TopProductDto>> ProjectTopProductsAsync(
        IQueryable<ECommerce.Domain.Entities.OrderItem> query,
        CancellationToken cancellationToken)
    {
        return await query
            .GroupBy(item => new { item.ProductId, item.ProductName })
            .Select(group => new TopProductDto(
                group.Key.ProductId,
                group.Key.ProductName,
                group.Sum(item => item.Quantity),
                group.Sum(item => item.UnitPrice * item.Quantity)))
            .OrderByDescending(product => product.UnitsSold)
            .ThenBy(product => product.ProductId)
            .Take(5)
            .ToListAsync(cancellationToken);
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
