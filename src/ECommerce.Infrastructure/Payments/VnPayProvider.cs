using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

// Tích hợp VNPay sandbox: tạo URL redirect ký HMAC-SHA512, verify return/IPN.
// Khi chưa cấu hình key, fallback hoàn tất tức thì (demo).
public class VnPayProvider : IPaymentProvider
{
    private readonly VnPayOptions _opt;
    private readonly bool _allowDemo;

    public VnPayProvider(IOptions<PaymentOptions> opt)
    {
        _opt = opt.Value.VnPay;
        _allowDemo = opt.Value.AllowDemo;
    }

    public string Key => "vnpay";

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext ctx, CancellationToken ct = default)
    {
        if (ctx.Amount <= 0)
            return Task.FromResult(new PaymentInitResult(false, null, "", "Invalid amount."));

        if (!_opt.Enabled)
        {
            // Chưa cấu hình key: chỉ demo ở môi trường Development, ngược lại từ chối.
            if (_allowDemo)
                return Task.FromResult(new PaymentInitResult(true, null, $"VNPAY-DEMO-{Guid.NewGuid():N}", null));
            return Task.FromResult(new PaymentInitResult(false, null, "", "VNPay chưa được cấu hình."));
        }

        var txnRef = $"{ctx.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var now = DateTime.UtcNow.AddHours(7); // giờ VN

        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = _opt.Version,
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _opt.TmnCode,
            ["vnp_Amount"] = ((long)(ctx.Amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = txnRef,
            ["vnp_OrderInfo"] = $"Thanh toan don hang {ctx.OrderId}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = ctx.ReturnUrl,
            ["vnp_IpAddr"] = string.IsNullOrEmpty(ctx.ClientIp) ? "127.0.0.1" : ctx.ClientIp,
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
        };

        var query = BuildQuery(data);
        var secureHash = HmacSha512(_opt.HashSecret, query);
        var redirectUrl = $"{_opt.BaseUrl}?{query}&vnp_SecureHash={secureHash}";

        return Task.FromResult(new PaymentInitResult(false, redirectUrl, txnRef, null));
    }

    public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var orderId = ParseOrderId(data);

        if (!_opt.Enabled)
        {
            if (_allowDemo)
                return Task.FromResult(new PaymentVerifyResult(true, orderId, $"VNPAY-DEMO-{Guid.NewGuid():N}", null));
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "VNPay chưa được cấu hình."));
        }

        if (!data.TryGetValue("vnp_SecureHash", out var receivedHash))
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "Missing secure hash."));

        var verifyData = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in data)
            if (k.StartsWith("vnp_") && k != "vnp_SecureHash" && k != "vnp_SecureHashType")
                verifyData[k] = v;

        var computed = HmacSha512(_opt.HashSecret, BuildQuery(verifyData));
        if (!string.Equals(computed, receivedHash, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new PaymentVerifyResult(false, orderId, "", "Invalid signature."));

        var responseCode = data.TryGetValue("vnp_ResponseCode", out var rc) ? rc : "";
        var txnRef = data.TryGetValue("vnp_TxnRef", out var tr) ? tr : "";
        var success = responseCode == "00";
        return Task.FromResult(new PaymentVerifyResult(success, orderId, txnRef,
            success ? null : $"VNPay response code {responseCode}"));
    }

    private static int ParseOrderId(IReadOnlyDictionary<string, string> data)
    {
        if (data.TryGetValue("vnp_TxnRef", out var txnRef))
        {
            var part = txnRef.Split('-')[0];
            if (int.TryParse(part, out var id)) return id;
        }
        return 0;
    }

    private static string BuildQuery(SortedDictionary<string, string> data) =>
        string.Join("&", data.Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

    private static string HmacSha512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
