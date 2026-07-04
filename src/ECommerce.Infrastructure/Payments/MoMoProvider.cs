using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

// Tích hợp MoMo AIO v2 sandbox: POST tạo giao dịch -> payUrl redirect, verify chữ ký HMAC-SHA256.
// Sandbox dùng test credentials công khai của MoMo nên chạy thật ngay không cần đăng ký.
public class MoMoProvider : IPaymentProvider
{
    private static readonly HttpClient Http = new();
    private readonly MoMoOptions _opt;
    private readonly bool _allowDemo;

    public MoMoProvider(IOptions<PaymentOptions> opt)
    {
        _opt = opt.Value.MoMo;
        _allowDemo = opt.Value.AllowDemo;
    }

    public string Key => "momo";

    public async Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        if (ctx.Amount <= 0)
            return new PaymentInitResult(false, null, "", "Invalid amount.");

        if (!_opt.Enabled)
            return _allowDemo
                ? new PaymentInitResult(true, null, $"MOMO-DEMO-{Guid.NewGuid():N}", null)
                : new PaymentInitResult(false, null, "", "MoMo chưa được cấu hình.");

        // MoMo yêu cầu số tiền là VND nguyên (>= 1.000). Quy đổi USD -> VND để demo.
        var amountVnd = (long)Math.Round(ctx.Amount * _opt.UsdToVndRate, MidpointRounding.AwayFromZero);
        if (amountVnd < 1000) amountVnd = 1000;

        var requestId = $"{ctx.OrderId}-{Guid.NewGuid():N}";
        var momoOrderId = requestId;
        var orderInfo = $"Thanh toan don hang {ctx.OrderId}";
        var redirectUrl = string.IsNullOrEmpty(ctx.ReturnUrl) ? _opt.RedirectUrl : ctx.ReturnUrl;
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

            return new PaymentInitResult(false, payUrl, requestId, null);
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
            if (_allowDemo)
                return Task.FromResult(new PaymentVerifyResult(true, orderId, $"MOMO-DEMO-{Guid.NewGuid():N}", null));
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "MoMo chưa được cấu hình."));
        }

        string G(string k) => data.TryGetValue(k, out var v) ? v : "";
        var rawSignature =
            $"accessKey={_opt.AccessKey}&amount={G("amount")}&extraData={G("extraData")}&message={G("message")}" +
            $"&orderId={G("orderId")}&orderInfo={G("orderInfo")}&orderType={G("orderType")}" +
            $"&partnerCode={G("partnerCode")}&payType={G("payType")}&requestId={G("requestId")}" +
            $"&responseTime={G("responseTime")}&resultCode={G("resultCode")}&transId={G("transId")}";
        var computed = HmacSha256(_opt.SecretKey, rawSignature);

        if (!string.Equals(computed, G("signature"), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "Invalid signature."));

        var success = G("resultCode") == "0";
        var transId = G("transId");
        return Task.FromResult(new PaymentVerifyResult(success, orderId, transId,
            success ? null : $"MoMo result code {G("resultCode")}"));
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
