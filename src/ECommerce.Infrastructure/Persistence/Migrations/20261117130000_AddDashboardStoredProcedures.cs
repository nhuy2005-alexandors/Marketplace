using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261117130000_AddDashboardStoredProcedures")]
public partial class AddDashboardStoredProcedures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Dashboard SQL migration requires local ECommerceDb_Dev.', 1;");

        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_GetOrderStatusSummary
                @SellerId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;

                SELECT
                    o.Status,
                    COUNT(*) AS [Count]
                FROM dbo.Orders AS o
                WHERE @SellerId IS NULL
                   OR EXISTS
                      (
                          SELECT 1
                          FROM dbo.OrderItems AS oi
                          WHERE oi.OrderId = o.Id
                            AND oi.SellerId = @SellerId
                      )
                GROUP BY o.Status
                ORDER BY o.Status;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_GetTopSellingProducts
                @SellerId int = NULL,
                @Take int = 5
            AS
            BEGIN
                SET NOCOUNT ON;

                DECLARE @SafeTake int =
                    CASE WHEN @Take BETWEEN 1 AND 20 THEN @Take ELSE 5 END;

                SELECT TOP (@SafeTake)
                    oi.ProductId,
                    oi.ProductName,
                    SUM(oi.Quantity) AS UnitsSold,
                    CONVERT(decimal(18,2), SUM(oi.UnitPrice * oi.Quantity)) AS Revenue
                FROM dbo.OrderItems AS oi
                INNER JOIN dbo.Orders AS o ON o.Id = oi.OrderId
                INNER JOIN dbo.Payments AS p ON p.OrderId = o.Id
                WHERE o.Status IN (1, 2, 3)
                  AND p.Status = 1
                  AND p.RequiresReconciliation = 0
                  AND p.IsDemo = 0
                  AND oi.Status <> 3
                  AND (@SellerId IS NULL OR oi.SellerId = @SellerId)
                GROUP BY oi.ProductId, oi.ProductName
                ORDER BY SUM(oi.Quantity) DESC, oi.ProductId ASC;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_OrderItems_ProtectSnapshot
            ON dbo.OrderItems
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM deleted d
                    LEFT JOIN inserted i ON i.Id = d.Id
                    WHERE i.Id IS NULL
                )
                    THROW 51008, 'Order item history cannot be physically deleted.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    INNER JOIN dbo.Products p ON p.Id = i.ProductId
                    WHERE d.Id IS NULL
                      AND (i.SellerId <> p.SellerId
                           OR CONVERT(varbinary(400), i.ProductName) <> CONVERT(varbinary(400), p.Name)
                           OR i.UnitPrice <> p.Price)
                )
                    THROW 51006, 'Order item checkout snapshot must match the product at insertion.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.OrderId <> d.OrderId
                       OR i.ProductId <> d.ProductId
                       OR i.SellerId <> d.SellerId
                       OR CONVERT(varbinary(400), i.ProductName) <> CONVERT(varbinary(400), d.ProductName)
                       OR i.UnitPrice <> d.UnitPrice
                       OR i.Quantity <> d.Quantity
                )
                    THROW 51007, 'Order item checkout snapshot fields are immutable.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Dashboard SQL migration requires local ECommerceDb_Dev.', 1;");
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetTopSellingProducts;");
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetOrderStatusSummary;");

        // Restore the previously accepted INSERT/UPDATE snapshot trigger when rolling back this migration.
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_OrderItems_ProtectSnapshot
            ON dbo.OrderItems
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    INNER JOIN dbo.Products p ON p.Id = i.ProductId
                    WHERE d.Id IS NULL
                      AND (i.SellerId <> p.SellerId
                           OR CONVERT(varbinary(400), i.ProductName) <> CONVERT(varbinary(400), p.Name)
                           OR i.UnitPrice <> p.Price)
                )
                    THROW 51006, 'Order item checkout snapshot must match the product at insertion.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.Id = i.Id
                    WHERE i.OrderId <> d.OrderId
                       OR i.ProductId <> d.ProductId
                       OR i.SellerId <> d.SellerId
                       OR CONVERT(varbinary(400), i.ProductName) <> CONVERT(varbinary(400), d.ProductName)
                       OR i.UnitPrice <> d.UnitPrice
                       OR i.Quantity <> d.Quantity
                )
                    THROW 51007, 'Order item checkout snapshot fields are immutable.', 1;
            END;
            """);
    }
}
