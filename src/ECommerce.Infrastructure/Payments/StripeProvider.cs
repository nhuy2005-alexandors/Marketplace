using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace ECommerce.Infrastructure.Payments;

// Tích hợp Stripe Checkout: tạo Session redirect, verify qua session_id.
// Khi chưa cấu hình SecretKey, fallback hoàn tất tức thì (demo).
public class StripeProvider : IPaymentProvider
{
    private readonly StripeOptions _opt;

    public StripeProvider(IOptions<PaymentOptions> opt) => _opt = opt.Value.Stripe;

    public string Key => "stripe";

    public async Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        if (ctx.Amount <= 0)
            return new PaymentInitResult(false, null, "", "Invalid amount.");

        if (!_opt.Enabled)
            return new PaymentInitResult(true, null, $"STRIPE-DEMO-{Guid.NewGuid():N}", null);

        StripeConfiguration.ApiKey = _opt.SecretKey;
        var successUrl = string.IsNullOrEmpty(ctx.ReturnUrl) ? _opt.SuccessUrl : ctx.ReturnUrl;

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{successUrl}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = _opt.CancelUrl,
            ClientReferenceId = ctx.OrderId.ToString(),
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = (long)(ctx.Amount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Order #{ctx.OrderId}"
                        }
                    }
                }
            },
            Metadata = new Dictionary<string, string> { ["orderId"] = ctx.OrderId.ToString() }
        };

        try
        {
            var session = await new SessionService().CreateAsync(options, cancellationToken: ct);
            return new PaymentInitResult(false, session.Url, session.Id, null);
        }
        catch (StripeException ex)
        {
            return new PaymentInitResult(false, null, "", ex.Message);
        }
    }

    public async Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var sessionId = data.TryGetValue("session_id", out var sid) ? sid : "";

        if (!_opt.Enabled)
        {
            var demoOrderId = data.TryGetValue("orderId", out var o) && int.TryParse(o, out var di) ? di : 0;
            return new PaymentVerifyResult(true, demoOrderId, $"STRIPE-DEMO-{Guid.NewGuid():N}", null);
        }

        if (string.IsNullOrEmpty(sessionId))
            return new PaymentVerifyResult(false, 0, "", "Missing session_id.");

        StripeConfiguration.ApiKey = _opt.SecretKey;
        try
        {
            var session = await new SessionService().GetAsync(sessionId, cancellationToken: ct);
            var orderId = int.TryParse(session.ClientReferenceId, out var id) ? id : 0;
            var paid = session.PaymentStatus == "paid";
            return new PaymentVerifyResult(paid, orderId, session.PaymentIntentId ?? sessionId,
                paid ? null : "Payment not completed.");
        }
        catch (StripeException ex)
        {
            return new PaymentVerifyResult(false, 0, "", ex.Message);
        }
    }
}
