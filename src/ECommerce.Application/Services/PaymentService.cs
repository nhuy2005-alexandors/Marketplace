using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IAppDbContext _db;
    private readonly IPaymentProviderFactory _providers;

    public PaymentService(IAppDbContext db, IPaymentProviderFactory providers)
    {
        _db = db;
        _providers = providers;
    }

    public async Task<Result<PayResultDto>> InitiateAsync(int userId, int orderId, PayOrderRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PaymentMethod>(MapToMethod(r.Method), true, out var method))
            return Result.Fail<PayResultDto>("Invalid payment method.", ErrorType.Validation);

        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return Result.Fail<PayResultDto>("Order not found.", ErrorType.NotFound);
        if (order.UserId != userId)
            return Result.Fail<PayResultDto>("Access denied.", ErrorType.Forbidden);
        if (order.Status != OrderStatus.Pending)
            return Result.Fail<PayResultDto>($"Order cannot be paid in {order.Status} state.", ErrorType.Conflict);

        var amount = order.Total;
        var provider = _providers.Resolve(r.Method);

        var payment = order.Payment ?? new Payment { OrderId = order.Id };
        payment.Amount = amount;
        payment.Method = method;
        payment.Status = PaymentStatus.Pending;
        if (order.Payment is null) _db.Payments.Add(payment);
        order.Payment = payment;

        var init = await provider.CreatePaymentAsync(
            new PaymentContext(order.Id, amount, r.ReturnUrl ?? "", "0.0.0.0"), ct);

        if (!string.IsNullOrEmpty(init.Error))
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return Result.Fail<PayResultDto>(init.Error, ErrorType.Conflict);
        }

        if (!init.Completed && !string.IsNullOrEmpty(init.RedirectUrl))
        {
            payment.TransactionId = init.TransactionId;
            await _db.SaveChangesAsync(ct);
            return Result.Ok(new PayResultDto(true, init.RedirectUrl, null));
        }

        // Hoàn tất ngay (mock/COD)
        var finalize = Finalize(order, payment, init.TransactionId);
        if (!finalize.Success)
            return Result.Fail<PayResultDto>(finalize.Error!, finalize.ErrorType);
        await _db.SaveChangesAsync(ct);
        return Result.Ok(new PayResultDto(false, null, order.ToDto()));
    }

    public async Task<Result<OrderDto>> ConfirmAsync(string provider, IReadOnlyDictionary<string, string> callbackData, CancellationToken ct = default)
    {
        var p = _providers.Resolve(provider);
        var verify = await p.VerifyAsync(callbackData, ct);

        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == verify.OrderId, ct);
        if (order is null)
            return Result.Fail<OrderDto>("Order not found.", ErrorType.NotFound);

        var payment = order.Payment ?? new Payment { OrderId = order.Id, Amount = order.Total };
        if (order.Payment is null) _db.Payments.Add(payment);

        if (!verify.Success)
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return Result.Fail<OrderDto>(verify.Error ?? "Payment verification failed.", ErrorType.Conflict);
        }

        if (order.Status == OrderStatus.Paid)
            return Result.Ok(order.ToDto());

        var finalize = Finalize(order, payment, verify.TransactionId);
        if (!finalize.Success)
            return Result.Fail<OrderDto>(finalize.Error!, finalize.ErrorType);
        await _db.SaveChangesAsync(ct);
        return Result.Ok(order.ToDto());
    }

    private static Result Finalize(Order order, Payment payment, string transactionId)
    {
        payment.Status = PaymentStatus.Completed;
        payment.TransactionId = transactionId;
        payment.PaidAt = DateTime.UtcNow;
        try
        {
            order.ChangeStatus(OrderStatus.Paid);
        }
        catch (InvalidOrderTransitionException ex)
        {
            return Result.Fail(ex.Message, ErrorType.Conflict);
        }
        return Result.Ok();
    }

    // map provider key -> PaymentMethod enum cho lưu trữ
    private static string MapToMethod(string method) => method.ToLowerInvariant() switch
    {
        "vnpay" => "CreditCard",
        "stripe" => "CreditCard",
        "cod" => "CashOnDelivery",
        "mock" => "CreditCard",
        _ => method
    };
}
