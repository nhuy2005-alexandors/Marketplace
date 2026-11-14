using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations;

public partial class OwnerCatalogIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Owner catalog integrity migration requires local ECommerceDb_Dev.', 1;");

        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM OrderItems oi
                LEFT JOIN Users u ON u.Id = oi.SellerId
                WHERE u.Id IS NULL
            )
                THROW 51001, 'Cannot add OrderItems seller FK: orphan seller IDs exist.', 1;

            IF EXISTS (SELECT 1 FROM Orders WHERE Status NOT IN (0, 1, 2, 3, 4, 5))
                THROW 51002, 'Cannot constrain Orders.Status: unknown enum values exist.', 1;

            IF EXISTS (SELECT 1 FROM OrderItems WHERE Status NOT IN (0, 1, 2, 3))
                THROW 51003, 'Cannot constrain OrderItems.Status: unknown enum values exist.', 1;

            IF EXISTS (SELECT 1 FROM Payments WHERE Status NOT IN (0, 1, 2, 3, 4))
                THROW 51004, 'Cannot constrain Payments.Status: unknown enum values exist.', 1;

            IF EXISTS (SELECT 1 FROM Payments WHERE Method NOT IN (0, 1, 2, 3))
                THROW 51005, 'Cannot constrain Payments.Method: unknown enum values exist.', 1;
            """);

        migrationBuilder.AddForeignKey(
            name: "FK_OrderItems_Users_SellerId",
            table: "OrderItems",
            column: "SellerId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Orders_Status",
            table: "Orders",
            sql: "[Status] IN (0, 1, 2, 3, 4, 5)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_OrderItems_Status",
            table: "OrderItems",
            sql: "[Status] IN (0, 1, 2, 3)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Payments_Status",
            table: "Payments",
            sql: "[Status] IN (0, 1, 2, 3, 4)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Payments_Method",
            table: "Payments",
            sql: "[Method] IN (0, 1, 2, 3)");

        migrationBuilder.CreateIndex(
            name: "IX_ProductAttributeValues_AttributeDefinitionId_ValueNumber",
            table: "ProductAttributeValues",
            columns: new[] { "AttributeDefinitionId", "ValueNumber" },
            filter: "[ValueNumber] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProductAttributeValues_AttributeDefinitionId_ValueBoolean",
            table: "ProductAttributeValues",
            columns: new[] { "AttributeDefinitionId", "ValueBoolean" },
            filter: "[ValueBoolean] IS NOT NULL");

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
                           OR i.ProductName <> p.Name
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
                       OR i.ProductName <> d.ProductName
                       OR i.UnitPrice <> d.UnitPrice
                       OR i.Quantity <> d.Quantity
                )
                    THROW 51007, 'Order item checkout snapshot fields are immutable.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Owner catalog integrity migration requires local ECommerceDb_Dev.', 1;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_OrderItems_ProtectSnapshot;");

        migrationBuilder.DropIndex(
            name: "IX_ProductAttributeValues_AttributeDefinitionId_ValueNumber",
            table: "ProductAttributeValues");
        migrationBuilder.DropIndex(
            name: "IX_ProductAttributeValues_AttributeDefinitionId_ValueBoolean",
            table: "ProductAttributeValues");

        migrationBuilder.DropCheckConstraint(name: "CK_Orders_Status", table: "Orders");
        migrationBuilder.DropCheckConstraint(name: "CK_OrderItems_Status", table: "OrderItems");
        migrationBuilder.DropCheckConstraint(name: "CK_Payments_Status", table: "Payments");
        migrationBuilder.DropCheckConstraint(name: "CK_Payments_Method", table: "Payments");

        migrationBuilder.DropForeignKey(
            name: "FK_OrderItems_Users_SellerId",
            table: "OrderItems");
    }
}
