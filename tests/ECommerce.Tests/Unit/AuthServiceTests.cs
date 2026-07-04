using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Auth;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class AuthServiceTests
{
    private sealed class StubJwt : IJwtTokenGenerator
    {
        public string Generate(User user) => $"token-{user.Id}";
        public (string Token, DateTime ExpiresAt) GenerateRefreshToken() =>
            ($"refresh-{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(7));
    }

    private static AuthService Build(Infrastructure.Persistence.AppDbContext db) =>
        new(db, new BCryptPasswordHasher(), new StubJwt());

    [Fact]
    public async Task Register_CreatesUser_AndReturnsToken()
    {
        using var db = TestDb.Create();
        var auth = Build(db);

        var result = await auth.RegisterAsync(new RegisterRequest("New@X.com", "secret1", "New User"));

        result.Success.Should().BeTrue();
        result.Value!.Token.Should().NotBeNullOrEmpty();
        result.Value.User.Email.Should().Be("new@x.com");
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsConflict()
    {
        using var db = TestDb.Create();
        var auth = Build(db);
        await auth.RegisterAsync(new RegisterRequest("dup@x.com", "secret1", "A"));

        var result = await auth.RegisterAsync(new RegisterRequest("dup@x.com", "secret2", "B"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Login_WrongPassword_IsUnauthorized()
    {
        using var db = TestDb.Create();
        var auth = Build(db);
        await auth.RegisterAsync(new RegisterRequest("a@x.com", "correct", "A"));

        var result = await auth.LoginAsync(new LoginRequest("a@x.com", "wrong"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Login_CorrectPassword_Succeeds()
    {
        using var db = TestDb.Create();
        var auth = Build(db);
        await auth.RegisterAsync(new RegisterRequest("a@x.com", "correct", "A"));

        var result = await auth.LoginAsync(new LoginRequest("a@x.com", "correct"));

        result.Success.Should().BeTrue();
        result.Value!.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndReturnsNewPair()
    {
        using var db = TestDb.Create();
        var auth = Build(db);
        var login = (await auth.RegisterAsync(new RegisterRequest("a@x.com", "correct", "A"))).Value!;

        var refreshed = await auth.RefreshAsync(login.RefreshToken);

        refreshed.Success.Should().BeTrue();
        refreshed.Value!.RefreshToken.Should().NotBe(login.RefreshToken); // rotated

        // Token cũ bị thu hồi -> dùng lại thất bại.
        var reuse = await auth.RefreshAsync(login.RefreshToken);
        reuse.Success.Should().BeFalse();
        reuse.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Unauthorized()
    {
        using var db = TestDb.Create();
        var auth = Build(db);

        var result = await auth.RefreshAsync("nonexistent");

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var db = TestDb.Create();
        var auth = Build(db);
        var login = (await auth.RegisterAsync(new RegisterRequest("a@x.com", "correct", "A"))).Value!;

        await auth.LogoutAsync(login.RefreshToken);

        var result = await auth.RefreshAsync(login.RefreshToken);
        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Unauthorized);
    }
}
