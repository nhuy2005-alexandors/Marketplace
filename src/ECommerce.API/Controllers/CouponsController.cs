using ECommerce.Application.DTOs.Coupons;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public class CouponsController : ApiControllerBase
{
    private readonly ICouponService _coupons;

    public CouponsController(ICouponService coupons) => _coupons = coupons;

    [Authorize]
    [HttpPost("validate")]
    [ProducesResponseType(typeof(CouponPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CouponPreviewDto>> Validate(ValidateCouponRequest request, CancellationToken ct)
        => ToResponse(await _coupons.ValidateAsync(request, ct));

    [AllowAnonymous]
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<CouponDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CouponDto>>> GetActive(CancellationToken ct)
        => Ok(await _coupons.GetActiveAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CouponDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CouponDto>>> GetAll(CancellationToken ct)
        => Ok(await _coupons.GetAllAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(CouponDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CouponDto>> Create(CreateCouponRequest request, CancellationToken ct)
        => ToResponse(await _coupons.CreateAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
        => ToResponse(await _coupons.DeleteAsync(id, ct));
}
