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
    public async Task<ActionResult<CouponPreviewDto>> Validate(ValidateCouponRequest request, CancellationToken ct)
        => ToResponse(await _coupons.ValidateAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CouponDto>>> GetAll(CancellationToken ct)
        => Ok(await _coupons.GetAllAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CouponDto>> Create(CreateCouponRequest request, CancellationToken ct)
        => ToResponse(await _coupons.CreateAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
        => ToResponse(await _coupons.DeleteAsync(id, ct));
}
