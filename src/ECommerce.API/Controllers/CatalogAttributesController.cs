using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public class CatalogAttributesController : ApiControllerBase
{
    private readonly IAttributeDefinitionService _attributes;

    public CatalogAttributesController(IAttributeDefinitionService attributes) => _attributes = attributes;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AttributeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AttributeDefinitionDto>>> GetAll(
        [FromQuery] int? categoryId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        if (includeInactive && !IsAdmin) return Forbid();
        return Ok(await _attributes.GetAllAsync(categoryId, includeInactive, ct));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(AttributeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttributeDefinitionDto>> Create(CreateAttributeDefinitionRequest request, CancellationToken ct)
        => ToResponse(await _attributes.CreateAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AttributeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttributeDefinitionDto>> Update(int id, UpdateAttributeDefinitionRequest request, CancellationToken ct)
        => ToResponse(await _attributes.UpdateAsync(id, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
        => ToResponse(await _attributes.DeleteAsync(id, ct));
}
