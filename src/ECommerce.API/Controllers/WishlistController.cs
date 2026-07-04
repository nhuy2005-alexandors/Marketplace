using ECommerce.Application.DTOs.Wishlist;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize]
public class WishlistController : ApiControllerBase
{
    private readonly IWishlistService _wishlist;

    public WishlistController(IWishlistService wishlist) => _wishlist = wishlist;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WishlistItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WishlistItemDto>>> Get(CancellationToken ct)
        => Ok(await _wishlist.GetAsync(UserId, ct));

    [HttpPost("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Add(int productId, CancellationToken ct)
        => ToResponse(await _wishlist.AddAsync(UserId, productId, ct));

    [HttpDelete("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Remove(int productId, CancellationToken ct)
        => ToResponse(await _wishlist.RemoveAsync(UserId, productId, ct));
}
