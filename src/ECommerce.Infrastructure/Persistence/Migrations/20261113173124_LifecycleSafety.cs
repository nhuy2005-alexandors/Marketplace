using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LifecycleSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Lifecycle migration requires local ECommerceDb_Dev.', 1;");
            migrationBuilder.AddColumn<decimal>(
                name: "ProviderAmount",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: true);


            migrationBuilder.AddColumn<string>(
                name: "ProviderCurrency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "Payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReconciliation",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Correct only invalid legacy SKU values; preserve every existing valid SKU.
            migrationBuilder.Sql("""
                UPDATE p SET Sku = CONCAT(N'LEGACY-', p.Id,
                    CASE WHEN EXISTS (SELECT 1 FROM Products q WHERE q.Id <> p.Id AND q.Sku = CONCAT(N'LEGACY-', p.Id))
                         THEN CONCAT(N'-', CONVERT(nvarchar(36), NEWID())) ELSE N'' END)
                FROM Products p WHERE LEN(LTRIM(RTRIM(p.Sku))) = 0;
                """);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RefundPending",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE o SET RefundPending = 1 FROM Orders o WHERE EXISTS (SELECT 1 FROM Refunds r WHERE r.OrderId = o.Id AND r.Status = 0);");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderKey_TransactionId",
                table: "Payments",
                columns: new[] { "ProviderKey", "TransactionId" },
                unique: true,
                filter: "[ProviderKey] IS NOT NULL AND [TransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderKey_ProviderReference",
                table: "Payments",
                columns: new[] { "ProviderKey", "ProviderReference" },
                unique: true,
                filter: "[ProviderKey] IS NOT NULL AND [ProviderReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Payments_ProviderKey_TransactionId", table: "Payments");
            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderKey_ProviderReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderCurrency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RequiresReconciliation",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundPending",
                table: "Orders");
        }
    }
}
