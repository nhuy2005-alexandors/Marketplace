using ECommerce.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ECommerce.Tests.Unit;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task BadHttpRequestPreserves413AndReturnsSafeBody()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new BadHttpRequestException("private Kestrel parser detail", StatusCodes.Status413PayloadTooLarge),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Request body is too large.", json.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("private Kestrel parser detail", json.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OtherExceptionsRemain500WithSafeBody()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("private internal details"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("private internal details", json.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
