using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

// Tích hợp MoMo AIO v2 sandbox: POST tạo giao dịch -> payUrl redirect, verify chữ ký HMAC-SHA256.
// Real initiation requires configured credentials and explicit catalog currency/FX.
public class MoMoProvider : IPaymentProvider
{
    private static readonly HttpClient Http = new();
    private readonly MoMoOptions _opt;

    public MoMoProvider(IOptions<PaymentOptions> opt)
    {
        _opt = opt.Value.MoMo;
    }

    public string Key => "momo";

    public PaymentQuote Quote(PaymentContext ctx)
    {
        if (ctx.Amount <= 0)
            return new(0, "VND", "Invalid amount.");

        if (!_opt.Enabled)
            return new(0, "VND", "MoMo chưa được cấu hình.");

        // USD is the canonical catalog currency. MoMo receives a frozen whole-VND quote
        // using an explicit VND-per-USD rate; the legacy UsdToVndRate is never consulted.
        var catalogCurrency = _opt.CatalogCurrency?.Trim().ToUpperInvariant();
        if (catalogCurrency != "USD" || _opt.CatalogToVndRate is null or <= 0)
            return new(0, "VND", "MoMo requires USD catalog currency and an explicit USD-to-VND rate (VND per USD).");
        decimal converted;
        try { converted = decimal.Round(ctx.Amount * _opt.CatalogToVndRate.Value, 0, MidpointRounding.AwayFromZero); }
        catch (OverflowException) { return new(0, "VND", "Provider amount exceeds supported range."); }
        if (converted < 1000 || converted > 50_000_000)
            return new(0, "VND", "MoMo amount must be between 1000 and 50000000 VND.");
        return new(converted, "VND", null);
    }

    public async Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        var quote = Quote(ctx);
        if (quote.Error is not null) return new(false, null, "", quote.Error);
        var amountVnd = decimal.ToInt64(quote.Amount);

        var requestId = ctx.RequestId ?? $"{ctx.OrderId}-{Guid.NewGuid():N}";
        var momoOrderId = requestId;
        var orderInfo = $"Thanh toan don hang {ctx.OrderId}";
        // The provider return endpoint must come from trusted server configuration.
        var redirectUrl = _opt.RedirectUrl;
        // payWithMethod: cổng hiện đủ QR ví + thẻ ATM nội địa + thẻ tín dụng.
        const string requestType = "payWithMethod";
        var extraData = "";

        var rawSignature =
            $"accessKey={_opt.AccessKey}&amount={amountVnd}&extraData={extraData}&ipnUrl={_opt.IpnUrl}" +
            $"&orderId={momoOrderId}&orderInfo={orderInfo}&partnerCode={_opt.PartnerCode}" +
            $"&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";
        var signature = HmacSha256(_opt.SecretKey, rawSignature);

        var body = new Dictionary<string, object>
        {
            ["partnerCode"] = _opt.PartnerCode,
            ["accessKey"] = _opt.AccessKey,
            ["requestId"] = requestId,
            ["amount"] = amountVnd.ToString(),
            ["orderId"] = momoOrderId,
            ["orderInfo"] = orderInfo,
            ["redirectUrl"] = redirectUrl,
            ["ipnUrl"] = _opt.IpnUrl,
            ["extraData"] = extraData,
            ["requestType"] = requestType,
            ["signature"] = signature,
            ["lang"] = "vi",
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.Endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            using var res = await Http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var resultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
            if (resultCode != 0)
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "MoMo error.";
                return new PaymentInitResult(false, null, "", msg ?? "MoMo error.");
            }

            var payUrl = root.TryGetProperty("payUrl", out var pu) ? pu.GetString() : null;
            if (string.IsNullOrEmpty(payUrl))
                return new PaymentInitResult(false, null, "", "MoMo không trả về payUrl.");

            return new PaymentInitResult(false, payUrl, requestId, null, amountVnd, "VND");
        }
        catch (Exception ex)
        {
            return new PaymentInitResult(false, null, "", $"MoMo request failed: {ex.Message}");
        }
    }

    public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var orderId = ParseOrderId(data);

        if (!_opt.Enabled)
        {
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "MoMo chưa được cấu hình."));
        }

        string G(string k) => data.TryGetValue(k, out var v) ? v : "";
        var rawSignature =
            $"accessKey={_opt.AccessKey}&amount={G("amount")}&extraData={G("extraData")}&message={G("message")}" +
            $"&orderId={G("orderId")}&orderInfo={G("orderInfo")}&orderType={G("orderType")}" +
            $"&partnerCode={G("partnerCode")}&payType={G("payType")}&requestId={G("requestId")}" +
            $"&responseTime={G("responseTime")}&resultCode={G("resultCode")}&transId={G("transId")}";
        var computed = HmacSha256(_opt.SecretKey, rawSignature);

        byte[] supplied;
        try { supplied = Convert.FromHexString(G("signature")); }
        catch (FormatException) { supplied = []; }
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(computed), supplied))
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "Invalid signature."));

        if (G("partnerCode") != _opt.PartnerCode || G("requestId") != G("orderId") || orderId <= 0 ||
            !decimal.TryParse(G("amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount < 1000 || amount > 50_000_000 ||
            !int.TryParse(G("resultCode"), out var resultCode) ||
            !long.TryParse(G("transId"), out var transaction) || (resultCode == 0 && transaction <= 0))
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "Invalid signed payment fields."));
        var success = G("resultCode") == "0";
        var transId = G("transId");
        return Task.FromResult(new PaymentVerifyResult(success, orderId, transId,
            success ? null : $"MoMo result code {G("resultCode")}", true, G("requestId"), amount, "VND"));
    }

    private static int ParseOrderId(IReadOnlyDictionary<string, string> data)
    {
        if (data.TryGetValue("orderId", out var oid))
        {
            var part = oid.Split('-')[0];
            if (int.TryParse(part, out var id)) return id;
        }
        return 0;
    }

    private static string HmacSha256(string key, string input)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
