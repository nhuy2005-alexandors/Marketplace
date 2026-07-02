using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
        => ToResponse(await _auth.RegisterAsync(request, ct));

    [HttpPost("register-seller")]
    public async Task<ActionResult<AuthResponse>> RegisterSeller(RegisterSellerRequest request, CancellationToken ct)
        => ToResponse(await _auth.RegisterSellerAsync(request, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        => ToResponse(await _auth.LoginAsync(request, ct));

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => ToResponse(await _auth.GetCurrentAsync(UserId, ct));
}
