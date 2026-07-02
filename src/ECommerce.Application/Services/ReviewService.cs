using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Reviews;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IAppDbContext _db;

    public ReviewService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReviewDto>> GetForProductAsync(int productId, CancellationToken ct = default)
    {
        var reviews = await _db.Reviews
            .Include(r => r.User)
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return reviews.Select(r => r.ToDto()).ToList();
    }

    public async Task<Result<ReviewDto>> CreateAsync(int userId, int productId, CreateReviewRequest r, CancellationToken ct = default)
    {
        if (r.Rating < 1 || r.Rating > 5)
            return Result.Fail<ReviewDto>("Rating must be between 1 and 5.", ErrorType.Validation);
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
            return Result.Fail<ReviewDto>("Product not found.", ErrorType.NotFound);

        var hasPurchased = await _db.Orders
            .Where(o => o.UserId == userId && o.Status != OrderStatus.Cancelled)
            .AnyAsync(o => o.Items.Any(i => i.ProductId == productId), ct);
        if (!hasPurchased)
            return Result.Fail<ReviewDto>("You can only review products you have purchased.", ErrorType.Forbidden);

        if (await _db.Reviews.AnyAsync(rv => rv.ProductId == productId && rv.UserId == userId, ct))
            return Result.Fail<ReviewDto>("You have already reviewed this product.", ErrorType.Conflict);

        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = r.Rating,
            Comment = r.Comment?.Trim()
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(review).Reference(rv => rv.User).LoadAsync(ct);
        return Result.Ok(review.ToDto());
    }
}
