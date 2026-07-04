using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Integration;

// Factory dùng chung cho integration test: thay AppDbContext (SqlServer) bằng
// SQLite in-memory qua 1 connection mở suốt đời factory (SQLite in-memory mất data
// nếu connection đóng, nên phải giữ connection sống và share cho toàn bộ request).
//
// Vì sao SQLite (không phải EF InMemory)? Program.cs gọi DbSeeder.SeedAsync -> db.Database.MigrateAsync()
// KHÔNG ĐIỀU KIỆN (không guard theo Environment). Provider "InMemory" của EF không hỗ trợ MigrateAsync
// (throw ngay). Do đó chọn SQLite (relational, hỗ trợ Migrate) để Program.cs chạy nguyên vẹn, không đụng
// production code.
//
// SQLite không hiểu các kiểu SQL Server (nvarchar(max), rowversion...) nên KHÔNG THỂ chạy thẳng các migration
// script gốc. Giải pháp: EnsureCreated() để SQLite tự sinh schema từ model hiện tại (theo type convention
// của SQLite), sau đó tự "đóng dấu" bảng __EFMigrationsHistory với đủ tên các migration đã có — khiến
// MigrateAsync() sau đó thấy "đã áp dụng hết" và trở thành no-op an toàn. Nhờ vậy Program.cs's seeding path
// chạy y như thật, chỉ khác engine lưu trữ.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        // Đóng dấu lịch sử migration để MigrateAsync() trong Program.cs thành no-op.
        var history = db.GetService<IHistoryRepository>();
        db.Database.ExecuteSqlRaw(history.GetCreateIfNotExistsScript());
        foreach (var migrationId in db.Database.GetMigrations())
        {
            var insertScript = history.GetInsertScript(
                new HistoryRow(migrationId, typeof(AppDbContext).Assembly.GetName().Version!.ToString()));
            db.Database.ExecuteSqlRaw(insertScript);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                (d.ServiceType.FullName?.Contains("DbContextOptionsConfiguration") == true &&
                 d.ServiceType.FullName.Contains("AppDbContext"))).ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseSqlite(_connection)
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
