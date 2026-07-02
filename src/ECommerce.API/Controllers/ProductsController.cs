using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Reviews;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public class ProductsController : ApiControllerBase
{
    private readonly IProductService _products;
    private readonly IReviewService _reviews;
    private readonly IFileStorage _storage;

    public ProductsController(IProductService products, IReviewService reviews, IFileStorage storage)
    {
        _products = products;
        _reviews = reviews;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> Search([FromQuery] ProductQuery query, CancellationToken ct)
        => Ok(await _products.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
        => ToResponse(await _products.GetByIdAsync(id, ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
        => ToResponse(await _products.CreateAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> Update(int id, UpdateProductRequest request, CancellationToken ct)
        => ToResponse(await _products.UpdateAsync(id, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
        => ToResponse(await _products.DeleteAsync(id, ct));

    // Upload ảnh sản phẩm, trả URL để gán vào ImageUrl khi tạo/sửa.
    [Authorize(Roles = "Admin")]
    [HttpPost("upload-image")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<object>> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        await using var stream = file.OpenReadStream();
        var url = await _storage.SaveImageAsync(stream, file.FileName, ct);
        return Ok(new { url });
    }

    [HttpGet("{id:int}/reviews")]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> GetReviews(int id, CancellationToken ct)
        => Ok(await _reviews.GetForProductAsync(id, ct));

    [Authorize]
    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult<ReviewDto>> CreateReview(int id, CreateReviewRequest request, CancellationToken ct)
        => ToResponse(await _reviews.CreateAsync(UserId, id, request, ct));
}
