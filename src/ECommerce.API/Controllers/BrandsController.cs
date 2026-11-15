using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public class BrandsController : ApiControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands) => _brands = brands;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BrandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> GetAll(CancellationToken ct)
        => Ok(await _brands.GetAllAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Create(CreateBrandRequest request, CancellationToken ct)
        => ToResponse(await _brands.CreateAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Update(int id, UpdateBrandRequest request, CancellationToken ct)
        => ToResponse(await _brands.UpdateAsync(id, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
        => ToResponse(await _brands.DeleteAsync(id, ct));
}
