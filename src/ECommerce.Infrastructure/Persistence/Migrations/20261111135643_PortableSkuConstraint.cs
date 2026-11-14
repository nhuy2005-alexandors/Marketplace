using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortableSkuConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'SKU migration requires local ECommerceDb_Dev.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Sku",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Sku",
                table: "Products",
                sql: "LTRIM(RTRIM([Sku])) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'SKU migration requires local ECommerceDb_Dev.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Sku",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Sku",
                table: "Products",
                sql: "LEN(LTRIM(RTRIM([Sku]))) > 0");
        }
    }
}
