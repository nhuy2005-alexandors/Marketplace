using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Authorize]
public class CartController : ApiControllerBase
{
    private readonly ICartService _cart;

    public CartController(ICartService cart) => _cart = cart;

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct)
        => Ok(await _cart.GetAsync(UserId, ct));

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken ct)
        => ToResponse(await _cart.AddItemAsync(UserId, request, ct));

    [HttpPut("items/{itemId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int itemId, UpdateCartItemRequest request, CancellationToken ct)
        => ToResponse(await _cart.UpdateItemAsync(UserId, itemId, request, ct));

    [HttpDelete("items/{itemId:int}")]
    public async Task<ActionResult<CartDto>> RemoveItem(int itemId, CancellationToken ct)
        => ToResponse(await _cart.RemoveItemAsync(UserId, itemId, ct));

    [HttpDelete]
    public async Task<ActionResult> Clear(CancellationToken ct)
        => ToResponse(await _cart.ClearAsync(UserId, ct));
}
