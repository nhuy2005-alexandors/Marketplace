using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Catalog migration requires local ECommerceDb_Dev.', 1;");
            migrationBuilder.AddColumn<int>(
                name: "BrandId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Fresh upgrades from a populated pre-V2 schema need unique values
            // before the unique index. Applied V2 databases do not rerun this Up.
            migrationBuilder.Sql("UPDATE [Products] SET [Sku] = CONCAT(N'LEGACY-', [Id]) WHERE LEN(LTRIM(RTRIM([Sku]))) = 0;");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_Id_CategoryId",
                table: "Products",
                columns: new[] { "Id", "CategoryId" });

            migrationBuilder.CreateTable(
                name: "AttributeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsFilterable = table.Column<bool>(type: "bit", nullable: false),
                    IsComparable = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_AttributeDefinitions_Id_CategoryId_DataType", x => new { x.Id, x.CategoryId, x.DataType });
                    table.CheckConstraint("CK_AttributeDefinitions_DataType", "[DataType] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_AttributeDefinitions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AttributeDefinitionId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ValueBoolean = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeValues", x => x.Id);
                    table.CheckConstraint("CK_ProductAttributeValues_TypedValue", "([DataType] = 0 AND [ValueText] IS NOT NULL AND [ValueNumber] IS NULL AND [ValueBoolean] IS NULL) OR ([DataType] = 1 AND [ValueText] IS NULL AND [ValueNumber] IS NOT NULL AND [ValueBoolean] IS NULL) OR ([DataType] = 2 AND [ValueText] IS NULL AND [ValueNumber] IS NULL AND [ValueBoolean] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProductAttributeValues_AttributeDefinitions_AttributeDefinitionId_CategoryId_DataType",
                        columns: x => new { x.AttributeDefinitionId, x.CategoryId, x.DataType },
                        principalTable: "AttributeDefinitions",
                        principalColumns: new[] { "Id", "CategoryId", "DataType" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductAttributeValues_Products_ProductId_CategoryId",
                        columns: x => new { x.ProductId, x.CategoryId },
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "CategoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_Rating",
                table: "Reviews",
                sql: "[Rating] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Price",
                table: "Products",
                sql: "[Price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Stock",
                table: "Products",
                sql: "[Stock] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_Amounts",
                table: "Coupons",
                sql: "[Value] >= 0 AND [MinOrderAmount] >= 0 AND ([Type] = 1 OR ([Type] = 0 AND [Value] <= 100)) AND [TimesUsed] >= 0 AND ([MaxUses] IS NULL OR [MaxUses] >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_CategoryId_Code",
                table: "AttributeDefinitions",
                columns: new[] { "CategoryId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_AttributeDefinitionId_CategoryId_DataType",
                table: "ProductAttributeValues",
                columns: new[] { "AttributeDefinitionId", "CategoryId", "DataType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_ProductId_AttributeDefinitionId",
                table: "ProductAttributeValues",
                columns: new[] { "ProductId", "AttributeDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_ProductId_CategoryId",
                table: "ProductAttributeValues",
                columns: new[] { "ProductId", "CategoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandId",
                table: "Products",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'ECommerceDb_Dev' OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) NOT LIKE N'LOCALDB#%' THROW 51000, 'Catalog migration requires local ECommerceDb_Dev.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "ProductAttributeValues");

            migrationBuilder.DropTable(
                name: "AttributeDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_Rating",
                table: "Reviews");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_Id_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BrandId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Price",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Stock",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_Amounts",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "Products");
        }
    }
}
