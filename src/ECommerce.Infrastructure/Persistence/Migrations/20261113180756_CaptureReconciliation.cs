using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaptureReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Capture reconciliation migration requires local ECommerceDb_Dev.', 1;");
            migrationBuilder.AddColumn<decimal>(
                name: "ReconciliationAmount",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationCurrency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationReference",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReconciliationAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReconciliationCurrency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReconciliationReference",
                table: "Payments");
        }
    }
}
