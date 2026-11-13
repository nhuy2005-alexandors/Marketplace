using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class PaymentService(IAppDbContext db, IPaymentProviderFactory providers) : IPaymentService
{
    public async Task<Result<PayResultDto>> InitiateAsync(int userId, int orderId, PayOrderRequest request, CancellationToken ct = default)
    {
        var key = (request.Method ?? "").Trim().ToLowerInvariant();
        if (key is not ("mock" or "cod" or "momo"))
            return Result.Fail<PayResultDto>("Invalid payment method.");
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result.Fail<PayResultDto>("Order not found.", ErrorType.NotFound);
        if (order.UserId != userId) return Result.Fail<PayResultDto>("Access denied.", ErrorType.Forbidden);
        if (order.Status != OrderStatus.Pending || order.RefundPending || order.Total <= 0 ||
            order.Payment?.Status is PaymentStatus.Completed or PaymentStatus.Refunded ||
            order.Payment?.RequiresReconciliation == true ||
            (order.Payment?.ProviderReference is not null || order.Payment?.TransactionId is not null))
            return Result.Fail<PayResultDto>("Order cannot start another payment attempt.", ErrorType.Conflict);

        var provider = providers.Resolve(key);
        var reference = $"{order.Id}-{Guid.NewGuid():N}";
        var context = new PaymentContext(order.Id, order.Total, request.ReturnUrl ?? "", "0.0.0.0", reference);
        var quote = provider.Quote(context);
        if (quote.Error is not null) return Result.Fail<PayResultDto>(quote.Error, ErrorType.Conflict);
        var payment = order.Payment ?? new Payment { OrderId = order.Id };
        payment.Amount = order.Total;
        payment.Method = key switch { "cod" => PaymentMethod.CashOnDelivery, "momo" => PaymentMethod.EWallet, _ => PaymentMethod.CreditCard };
        payment.ProviderKey = key;
        payment.ProviderReference = reference;
        payment.ProviderAmount = quote.Amount;
        payment.ProviderCurrency = quote.Currency;
        payment.IsDemo = key == "mock";
        payment.Status = PaymentStatus.Pending;
        payment.TransactionId = null;
        payment.PaidAt = null;
        if (order.Payment is null) db.Payments.Add(payment);
        order.Payment = payment;
        OrderLifecycle.Touch(db, order);
        // The reservation wins the Order/Payment rowversion race before any provider
        // side effect. A reserved reference is immutable, even after a failure.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail<PayResultDto>("Another payment/cancellation won the reservation. Reload.", ErrorType.Conflict); }
        catch (DbUpdateException) { return Result.Fail<PayResultDto>("Payment reservation could not be committed.", ErrorType.Conflict); }

        var init = await provider.CreatePaymentAsync(context, ct);
        await db.Entry(payment).ReloadAsync(ct);
        await db.Entry(order).ReloadAsync(ct);
        if (payment.Status is PaymentStatus.Completed or PaymentStatus.Refunded)
            return Result.Ok(new PayResultDto(false,null,order.ToDto()));
        if (payment.ProviderReference != reference)
            return Result.Fail<PayResultDto>("Payment reservation changed; reconciliation required.", ErrorType.Conflict);
        if (init.Error is not null)
            payment.Status = PaymentStatus.Failed;
        else if (key == "cod" && order.Status == OrderStatus.Pending)
            order.ChangeStatus(OrderStatus.Accepted);
        else if (init.Completed)
        {
            payment.Status = PaymentStatus.Completed;
            payment.TransactionId = init.TransactionId;
            payment.PaidAt = DateTime.UtcNow;
            payment.RequiresReconciliation = order.Status != OrderStatus.Pending || order.RefundPending;
            if (!payment.RequiresReconciliation) order.ChangeStatus(OrderStatus.Paid);
        }
        else if (key == "momo" && (string.IsNullOrEmpty(init.RedirectUrl) || init.TransactionId != reference ||
                 init.ProviderAmount != quote.Amount || init.ProviderCurrency != quote.Currency))
        {
            payment.Status = PaymentStatus.Failed;
            init = init with { Error = "Provider returned an incomplete payment attempt." };
        }
        OrderLifecycle.Touch(db, order);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail<PayResultDto>("Payment changed during initiation. Reload; do not resend the payment.", ErrorType.Conflict); }
        if (init.Error is not null) return Result.Fail<PayResultDto>(init.Error, ErrorType.Conflict);
        if (order.Status == OrderStatus.Cancelled)
            return Result.Fail<PayResultDto>("Order was cancelled during payment initiation. Any capture requires reconciliation.", ErrorType.Conflict);
        return Result.Ok(new PayResultDto(!init.Completed && key != "cod", init.RedirectUrl, init.Completed || key == "cod" ? order.ToDto() : null));
    }

    public async Task<Result<OrderDto>> ConfirmAsync(string provider, IReadOnlyDictionary<string,string> data, CancellationToken ct = default)
    {
        // Demo/COD never accept public callbacks, even in Development.
        if (!string.Equals(provider, "momo", StringComparison.OrdinalIgnoreCase))
            return Result.Fail<OrderDto>("Unsupported callback provider.");
        var verified = await providers.Resolve("momo").VerifyAsync(data, ct);
        if (!verified.Authenticated)
            return Result.Fail<OrderDto>(verified.Error ?? "Invalid provider authentication.");
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == verified.OrderId, ct);
        if (order is null) return Result.Fail<OrderDto>("Order not found.", ErrorType.NotFound);
        var payment = order.Payment;
        // Pre-cutover MoMo stored the request ID in TransactionId and had no
        // frozen provider quote. A matching signed capture is money evidence,
        // not permission to infer historical FX or activate fulfillment.
        if (payment is { Method: PaymentMethod.EWallet, ProviderReference: null, ProviderKey: null } &&
            payment.Status is PaymentStatus.Pending or PaymentStatus.Failed &&
            !string.IsNullOrEmpty(payment.TransactionId) && payment.TransactionId == verified.RequestId &&
            verified.Success && !string.IsNullOrWhiteSpace(verified.TransactionId) &&
            verified.Amount is > 0 && verified.Currency == "VND")
        {
            payment.ProviderKey = "momo";
            payment.ProviderReference = verified.RequestId;
            payment.ProviderAmount = verified.Amount;
            payment.ProviderCurrency = verified.Currency;
            payment.TransactionId = verified.TransactionId;
            payment.Status = PaymentStatus.Completed;
            payment.RequiresReconciliation = true;
            payment.ReconciliationAmount = verified.Amount;
            payment.ReconciliationCurrency = verified.Currency;
            payment.ReconciliationReference = verified.TransactionId;
            payment.PaidAt = DateTime.UtcNow;
            OrderLifecycle.Touch(db, order);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Result.Fail<OrderDto>("Legacy capture reconciliation conflicted. Retry the callback.", ErrorType.Conflict); }
            return Result.Ok(order.ToDto());
        }
        if (payment is null || payment.Method != PaymentMethod.EWallet || payment.ProviderKey != "momo" ||
            string.IsNullOrEmpty(payment.ProviderReference) || payment.ProviderReference != verified.RequestId)
            return Result.Fail<OrderDto>("Callback does not match the stored payment attempt.", ErrorType.Conflict);

        var quoteMatches = payment.ProviderAmount is not null && payment.ProviderAmount == verified.Amount &&
            payment.ProviderCurrency == "VND" && verified.Currency == "VND" && payment.Amount == order.Total;
        var conflictingCapture = payment.Status is PaymentStatus.Completed or PaymentStatus.Refunded &&
            payment.TransactionId != verified.TransactionId;
        if (verified.Success && (!quoteMatches || conflictingCapture))
        {
            if (verified.Amount is null or <= 0 || verified.Amount > 50_000_000 || verified.Currency?.Length != 3 ||
                string.IsNullOrWhiteSpace(verified.TransactionId) || verified.TransactionId.Length > 100)
                return Result.Fail<OrderDto>("Invalid capture fields.");
            if (payment.RequiresReconciliation && payment.ReconciliationReference == verified.TransactionId &&
                payment.ReconciliationAmount == verified.Amount && payment.ReconciliationCurrency == verified.Currency)
                return Result.Ok(order.ToDto());
            // Preserve the frozen expected quote and any previous terminal capture.
            // The separate reconciliation fields retain the inconsistent observation.
            payment.RequiresReconciliation = true;
            payment.ReconciliationAmount = verified.Amount;
            payment.ReconciliationCurrency = verified.Currency;
            payment.ReconciliationReference = verified.TransactionId;
            if (payment.Status is not (PaymentStatus.Completed or PaymentStatus.Refunded))
            {
                payment.Status = PaymentStatus.Completed;
                payment.TransactionId = verified.TransactionId;
                payment.PaidAt = DateTime.UtcNow;
            }
            OrderLifecycle.Touch(db, order);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Result.Fail<OrderDto>("Capture reconciliation conflicted. Retry callback.", ErrorType.Conflict); }
            return Result.Ok(order.ToDto());
        }
        if (!quoteMatches && payment.Status is not (PaymentStatus.Completed or PaymentStatus.Refunded))
            return Result.Fail<OrderDto>("Callback amount/currency does not match the stored attempt.", ErrorType.Conflict);

        // Terminal captures never regress. A repeated success must identify the same capture.
        if (payment.Status is PaymentStatus.Completed or PaymentStatus.Refunded)
        {
            return Result.Ok(order.ToDto());
        }
        if (verified.Success)
        {
            if (string.IsNullOrWhiteSpace(verified.TransactionId))
                return Result.Fail<OrderDto>("Missing capture reference.");
            payment.Status = PaymentStatus.Completed;
            payment.TransactionId = verified.TransactionId;
            payment.PaidAt = DateTime.UtcNow;
            // Preserve evidence of money collected after cancellation. Never reactivate
            // fulfillment or consume inventory that cancellation already restored.
            payment.RequiresReconciliation = order.Status != OrderStatus.Pending || order.RefundPending ||
                order.Items.Count == 0 || order.Items.Any(i => i.Status != FulfillmentStatus.Pending);
            if (payment.RequiresReconciliation)
            {
                payment.ReconciliationAmount = verified.Amount;
                payment.ReconciliationCurrency = verified.Currency;
                payment.ReconciliationReference = verified.TransactionId;
            }
            if (!payment.RequiresReconciliation) order.ChangeStatus(OrderStatus.Paid);
        }
        else if (payment.Status == PaymentStatus.Pending)
            payment.Status = PaymentStatus.Failed;
        else return Result.Ok(order.ToDto());
        OrderLifecycle.Touch(db, order);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail<OrderDto>("Payment changed concurrently. Retry the callback.", ErrorType.Conflict); }
        catch (DbUpdateException) { return Result.Fail<OrderDto>("Payment capture could not be committed; reconcile the provider reference.", ErrorType.Conflict); }
        return Result.Ok(order.ToDto());
    }
}
