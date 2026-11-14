using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Refunds;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class RefundService : IRefundService
{
    private readonly IAppDbContext _db;

    public RefundService(IAppDbContext db) => _db = db;

    public async Task<Result<RefundDto>> RequestAsync(
        int userId,
        RequestRefundRequest request,
        CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5 || reason.Length > 1000)
            return Result.Fail<RefundDto>("Refund reason must contain 5 to 1000 characters.");

        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);

        if (order is null)
            return Result.Fail<RefundDto>("Order not found.", ErrorType.NotFound);
        if (order.UserId != userId)
            return Result.Fail<RefundDto>("Access denied.", ErrorType.Forbidden);
        if (order.Status != OrderStatus.Paid)
            return Result.Fail<RefundDto>("Only paid orders can request a refund.", ErrorType.Conflict);
        if (order.Payment is null || order.Payment.Status != PaymentStatus.Completed)
            return Result.Fail<RefundDto>("The order has no completed payment to refund.", ErrorType.Conflict);
        if (order.Payment.RequiresReconciliation || order.Payment.IsDemo)
            return Result.Fail<RefundDto>("Payment requires reconciliation before a refund.", ErrorType.Conflict);
        if (order.Payment.Method == PaymentMethod.CashOnDelivery)
            return Result.Fail<RefundDto>("COD refunds are not supported until cash collection is recorded.", ErrorType.Conflict);
        if (order.Payment.Amount <= 0 || order.Payment.Amount != order.Total)
            return Result.Fail<RefundDto>("The captured payment amount does not match the order total.", ErrorType.Conflict);
        if (order.Payment.ProviderAmount is null or <= 0 ||
            string.IsNullOrWhiteSpace(order.Payment.ProviderCurrency))
            return Result.Fail<RefundDto>("Original provider capture amount/currency is unavailable; the refund cannot be verified.", ErrorType.Conflict);
        if (order.Items.Count == 0 || order.Items.Any(i => i.Status != FulfillmentStatus.Pending))
            return Result.Fail<RefundDto>("A full refund can only be requested before fulfillment starts.", ErrorType.Conflict);

        if (await _db.Refunds.AnyAsync(r => r.OrderId == order.Id, ct))
            return Result.Fail<RefundDto>("A refund request already exists for this order.", ErrorType.Conflict);

        var refund = new Refund
        {
            OrderId = order.Id,
            Order = order,
            PaymentId = order.Payment.Id,
            Payment = order.Payment,
            Amount = order.Payment.Amount,
            Reason = reason,
            Status = RefundStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        _db.Refunds.Add(refund);
        order.RefundPending = true;
        OrderLifecycle.Touch(_db, order);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DuplicateRefundException)
        {
            // The unique OrderId index is the final guard against concurrent duplicate requests.
            return Result.Fail<RefundDto>("A refund request already exists for this order.", ErrorType.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<RefundDto>("Order changed concurrently. Reload and retry.", ErrorType.Conflict);
        }

        return Result.Ok(ToDto(refund));
    }

    public async Task<Result<PagedResult<RefundDto>>> GetForUserAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken ct = default,
        int[]? orderIds = null)
    {
        var query = _db.Refunds
            .AsNoTracking()
            .Include(r => r.Order).ThenInclude(o => o.User)
            .Include(r => r.Payment)
            .Include(r => r.ProcessedByUser)
            .Where(r => r.Order.UserId == userId);

        if (orderIds is { Length: > 100 })
            return Result.Fail<PagedResult<RefundDto>>("At most 100 order IDs can be requested.");
        if (orderIds is { Length: > 0 }) query = query.Where(r => orderIds.Contains(r.OrderId));

        return Result.Ok(await PageAsync(query, page, pageSize, ct));
    }

    public async Task<Result<PagedResult<RefundDto>>> GetAllAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Refund> query = _db.Refunds
            .AsNoTracking()
            .Include(r => r.Order).ThenInclude(o => o.User)
            .Include(r => r.Payment)
            .Include(r => r.ProcessedByUser);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RefundStatus>(status.Trim(), true, out var parsed) ||
                !Enum.IsDefined(typeof(RefundStatus), parsed))
                return Result.Fail<PagedResult<RefundDto>>("Invalid refund status.");
            query = query.Where(r => r.Status == parsed);
        }

        return Result.Ok(await PageAsync(query, page, pageSize, ct));
    }

    public async Task<Result<RefundDto>> CompleteAsync(
        int adminId,
        int refundId,
        CompleteRefundRequest request,
        CancellationToken ct = default)
    {
        var externalReference = request.ExternalReference?.Trim();
        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(externalReference) || externalReference.Length < 3 || externalReference.Length > 100)
            return Result.Fail<RefundDto>("Refund transaction reference must contain 3 to 100 characters.");
        if (note?.Length > 1000)
            return Result.Fail<RefundDto>("Refund note must not exceed 1000 characters.");

        var refund = await _db.Refunds
            .Include(r => r.Order).ThenInclude(o => o.Items)
            .Include(r => r.Order).ThenInclude(o => o.User)
            .Include(r => r.Payment)
            .Include(r => r.ProcessedByUser)
            .FirstOrDefaultAsync(r => r.Id == refundId, ct);

        if (refund is null)
            return Result.Fail<RefundDto>("Refund request not found.", ErrorType.NotFound);
        if (refund.Status != RefundStatus.Pending)
            return Result.Fail<RefundDto>($"Cannot complete a refund in {refund.Status} state.", ErrorType.Conflict);

        var admin = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == adminId && u.Role == UserRole.Admin, ct);
        if (admin is null)
            return Result.Fail<RefundDto>("Admin account not found.", ErrorType.Forbidden);

        if (refund.Order.Status != OrderStatus.Paid ||
            refund.Payment.Status != PaymentStatus.Completed ||
            refund.Payment.Method == PaymentMethod.CashOnDelivery ||
            refund.Payment.RequiresReconciliation || refund.Payment.IsDemo || !refund.Order.RefundPending ||
            refund.Payment.Amount != refund.Amount ||
            refund.Payment.ProviderAmount is null or <= 0 ||
            string.IsNullOrWhiteSpace(refund.Payment.ProviderCurrency) ||
            refund.Order.Items.Count == 0 ||
            refund.Order.Items.Any(i => i.Status != FulfillmentStatus.Pending))
        {
            return Result.Fail<RefundDto>(
                "The order is no longer eligible for a full refund. Check payment and fulfillment state.",
                ErrorType.Conflict);
        }

        var productIds = refund.Order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
        if (products.Count != productIds.Count)
            return Result.Fail<RefundDto>("One or more order products are missing; inventory was not changed.", ErrorType.Conflict);

        var byId = products.ToDictionary(p => p.Id);
        try
        {
            foreach (var item in refund.Order.Items)
            {
                byId[item.ProductId].Stock += item.Quantity;
                item.ChangeStatus(FulfillmentStatus.Cancelled);
            }

            refund.Order.ChangeStatus(OrderStatus.Cancelled);
            refund.Order.RefundPending = false;
            OrderLifecycle.Touch(_db, refund.Order);
            refund.Payment.Status = PaymentStatus.Refunded;
            refund.Complete(adminId, externalReference, note);
            refund.ProcessedByUser = admin;

            // One SaveChanges call keeps refund, payment, order, items and stock in one DB transaction.
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOrderTransitionException ex)
        {
            return Result.Fail<RefundDto>(ex.Message, ErrorType.Conflict);
        }
        catch (InvalidRefundTransitionException ex)
        {
            return Result.Fail<RefundDto>(ex.Message, ErrorType.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<RefundDto>("The order or inventory changed concurrently. Reload and retry.", ErrorType.Conflict);
        }
        catch (DbUpdateException)
        {
            return Result.Fail<RefundDto>("The refund could not be committed. Reload the request and retry.", ErrorType.Conflict);
        }

        return Result.Ok(ToDto(refund));
    }

    public async Task<Result<RefundDto>> RejectAsync(
        int adminId,
        int refundId,
        RejectRefundRequest request,
        CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5 || reason.Length > 1000)
            return Result.Fail<RefundDto>("Rejection reason must contain 5 to 1000 characters.");

        var refund = await _db.Refunds
            .Include(r => r.Order).ThenInclude(o => o.User)
            .Include(r => r.Payment)
            .Include(r => r.ProcessedByUser)
            .FirstOrDefaultAsync(r => r.Id == refundId, ct);

        if (refund is null)
            return Result.Fail<RefundDto>("Refund request not found.", ErrorType.NotFound);
        if (refund.Status != RefundStatus.Pending)
            return Result.Fail<RefundDto>($"Cannot reject a refund in {refund.Status} state.", ErrorType.Conflict);

        var admin = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == adminId && u.Role == UserRole.Admin, ct);
        if (admin is null)
            return Result.Fail<RefundDto>("Admin account not found.", ErrorType.Forbidden);

        try
        {
            refund.Reject(adminId, reason);
            refund.Order.RefundPending = false;
            OrderLifecycle.Touch(_db, refund.Order);
            refund.ProcessedByUser = admin;
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidRefundTransitionException ex)
        {
            return Result.Fail<RefundDto>(ex.Message, ErrorType.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<RefundDto>("The refund request changed concurrently. Reload and retry.", ErrorType.Conflict);
        }

        return Result.Ok(ToDto(refund));
    }

    private static async Task<PagedResult<RefundDto>> PageAsync(
        IQueryable<Refund> query,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var refunds = await query
            .OrderByDescending(r => r.RequestedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<RefundDto>
        {
            Items = refunds.Select(ToDto).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    private static RefundDto ToDto(Refund refund) => new(
        refund.Id,
        refund.OrderId,
        refund.Order.User?.FullName ?? string.Empty,
        refund.Amount,
        refund.Reason,
        refund.Status.ToString(),
        refund.RequestedAt,
        refund.ProcessedAt,
        refund.ExternalReference,
        refund.Note,
        refund.ProcessedByUser?.FullName,
        refund.Payment?.ProviderAmount,
        refund.Payment?.ProviderCurrency,
        refund.Payment?.Method.ToString(),
        refund.Payment?.TransactionId);
}
