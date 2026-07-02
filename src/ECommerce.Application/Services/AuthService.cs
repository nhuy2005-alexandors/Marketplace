using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Result.Fail<AuthResponse>("Email already registered.", ErrorType.Conflict);

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Role = UserRole.Customer
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Result.Ok(Build(user));
    }

    public async Task<Result<AuthResponse>> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Result.Fail<AuthResponse>("Email already registered.", ErrorType.Conflict);

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            ShopName = request.ShopName.Trim(),
            Role = UserRole.Seller
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Result.Ok(Build(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            return Result.Fail<AuthResponse>("Invalid email or password.", ErrorType.Unauthorized);

        return Result.Ok(Build(user));
    }

    public async Task<Result<UserDto>> GetCurrentAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null)
            return Result.Fail<UserDto>("User not found.", ErrorType.NotFound);
        return Result.Ok(ToDto(user));
    }

    private AuthResponse Build(User user) => new(_jwt.Generate(user), ToDto(user));

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role.ToString(), user.ShopName);
}
