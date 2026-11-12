using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261112233500_AddRefunds")]
public partial class AddRefunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Refunds",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OrderId = table.Column<int>(type: "int", nullable: false),
                PaymentId = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ProcessedBy = table.Column<int>(type: "int", nullable: true),
                ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Refunds", x => x.Id);
                table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                table.CheckConstraint("CK_Refunds_Status", "[Status] IN (0, 1, 2)");
                table.ForeignKey(
                    name: "FK_Refunds_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Refunds_Payments_PaymentId",
                    column: x => x.PaymentId,
                    principalTable: "Payments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Refunds_Users_ProcessedBy",
                    column: x => x.ProcessedBy,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_OrderId",
            table: "Refunds",
            column: "OrderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_PaymentId",
            table: "Refunds",
            column: "PaymentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_ProcessedBy",
            table: "Refunds",
            column: "ProcessedBy");

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_Status",
            table: "Refunds",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Refunds");
    }
}
