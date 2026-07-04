using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public class SellersController : ApiControllerBase
{
    private readonly IProductService _products;

    public SellersController(IProductService products) => _products = products;

    // Public seller shop info; product listing reuses GET /api/products?sellerId=.
    [HttpGet("{id:int}/shop")]
    [ProducesResponseType(typeof(SellerShopDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SellerShopDto>> GetShop(int id, CancellationToken ct)
        => ToResponse(await _products.GetSellerShopAsync(id, ct));
}
