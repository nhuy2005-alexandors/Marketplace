using System.Text;
using System.Threading.RateLimiting;
using ECommerce.API.Middleware;
using ECommerce.API.Services;
using ECommerce.Application;
using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Auth;
using ECommerce.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Override secret/config nhạy cảm bằng file local (gitignored) khi deploy.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Tải GEMINI_API_KEY từ .env ở repo root nếu chưa được cấu hình bằng environment/appsettings.
if (string.IsNullOrWhiteSpace(builder.Configuration["Gemini:ApiKey"]))
{
    for (DirectoryInfo? directory = new(builder.Environment.ContentRootPath); directory is not null; directory = directory.Parent)
    {
        var envPath = Path.Combine(directory.FullName, ".env");
        if (!File.Exists(envPath)) continue;

        var apiKeyLine = File.ReadLines(envPath)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("GEMINI_API_KEY=", StringComparison.OrdinalIgnoreCase));
        if (apiKeyLine is not null)
        {
            var apiKey = apiKeyLine[(apiKeyLine.IndexOf('=') + 1)..].Trim().Trim('"', '\'');
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Gemini:ApiKey"] = apiKey
                });
            }
        }
        break;
    }
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Demo payment (hoàn tất ngay không cần key thật) chỉ cho phép ở Development.
builder.Services.PostConfigure<ECommerce.Infrastructure.Payments.PaymentOptions>(o =>
    o.AllowDemo = builder.Environment.IsDevelopment());

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();

// Thống nhất lỗi validation về envelope { "error": "..." } (thay cho ProblemDetails RFC7807 mặc định).
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var error = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault() ?? "Invalid request.";
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error });
    };
});

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
        };
    });
builder.Services.AddAuthorization();

const string CorsPolicy = "AllowFrontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Rate limiting: global per-IP fixed window + policy "auth" chặt cho login/register (chống brute-force).
const string AuthPolicy = "auth";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(AuthPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ECommerce API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Auto-migrate + seed khi khởi động (idempotent) — khỏi phải chạy 'ef database update' tay.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedAsync(db, hasher);
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
