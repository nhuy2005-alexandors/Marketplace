using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

public class VnpayProvider : IPaymentProvider, IPaymentQueryProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(35) };
    private const string PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    private const string QueryUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    private const decimal MaxVnd = 9_999_999_999m;
    private readonly VnpayOptions _opt;
    private readonly HttpClient _http;
    public VnpayProvider(IOptions<PaymentOptions> options) : this(options, Http) { }
    public VnpayProvider(IOptions<PaymentOptions> options, HttpClient http)
    {
        _opt = options.Value.Vnpay;
        _http = http;
    }

    public string Key => "vnpay";
    public PaymentQuote Quote(PaymentContext context)
    {
        if (!_opt.Enabled) return new(0, "VND", "VNPAY Sandbox credentials are not configured.");
        if (_opt.CatalogCurrency != "USD" || _opt.CatalogToVndRate is null or <= 0 || context.Amount <= 0)
            return new(0, "VND", "VNPAY requires a positive USD amount and an explicit VND-per-USD rate.");
        decimal vnd;
        try { vnd = decimal.Round(context.Amount * _opt.CatalogToVndRate.Value, 0, MidpointRounding.AwayFromZero); }
        catch (OverflowException) { return new(0, "VND", "VNPAY amount exceeds the supported range."); }
        return vnd is < 1 or > MaxVnd ? new(0, "VND", "VNPAY amount exceeds the supported range.") : new(vnd, "VND", null);
    }

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default)
    {
        var quote = Quote(context);
        if (quote.Error is not null) return Task.FromResult(new PaymentInitResult(false, null, "", quote.Error));
        if (!ParseReference(context.RequestId, out var orderId, out var created) || orderId != context.OrderId ||
            !IPAddress.TryParse(context.ClientIp, out var ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) ||
            !Uri.TryCreate(_opt.ReturnUrl, UriKind.Absolute, out var returnUri) || returnUri.Scheme is not ("http" or "https"))
            return Task.FromResult(new PaymentInitResult(false, null, "", "Invalid VNPAY reference, client IP or server return URL."));
        var data = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = _opt.TmnCode,
            ["vnp_Amount"] = (quote.Amount * 100m).ToString("0", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND", ["vnp_TxnRef"] = context.RequestId!,
            ["vnp_OrderInfo"] = $"Thanh toan don hang {orderId}", ["vnp_OrderType"] = "other", ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = _opt.ReturnUrl, ["vnp_IpAddr"] = ip.ToString(),
            ["vnp_CreateDate"] = FormatDate(created), ["vnp_ExpireDate"] = FormatDate(created.AddMinutes(15))
        };
        var query = Canonical(data);
        return Task.FromResult(new PaymentInitResult(false, PaymentUrl + "?" + query + "&vnp_SecureHash=" + Sign(query),
            context.RequestId!, null, quote.Amount, "VND"));
    }

    public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !SignatureMatches(Canonical(data), Get(data, "vnp_SecureHash")))
            return Task.FromResult(Invalid("Invalid VNPAY signature or missing credentials."));
        return Task.FromResult(VerifyFields(data));
    }

    public async Task<PaymentVerifyResult> QueryAsync(string storedReference, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !ParseReference(storedReference, out _, out var created) ||
            !IPAddress.TryParse(_opt.QueryIpAddress, out _))
            return Invalid("VNPAY query credentials, reference or server IP are not configured.");
        var request = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = Guid.NewGuid().ToString("N"), ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "querydr",
            ["vnp_TmnCode"] = _opt.TmnCode, ["vnp_TxnRef"] = storedReference,
            ["vnp_TransactionDate"] = FormatDate(created), ["vnp_CreateDate"] = FormatDate(DateTimeOffset.UtcNow),
            ["vnp_IpAddr"] = _opt.QueryIpAddress, ["vnp_OrderInfo"] = "Query Marketplace payment"
        };
        request["vnp_SecureHash"] = Sign(string.Join("|", new[] { "vnp_RequestId", "vnp_Version", "vnp_Command",
            "vnp_TmnCode", "vnp_TxnRef", "vnp_TransactionDate", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo" }.Select(k => request[k])));
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, QueryUrl)
            { Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json") };
            // The sandbox rejects requests without an application User-Agent with HTTP403 HTML.
            message.Headers.UserAgent.ParseAdd("Marketplace-Sandbox/1.0");
            message.Headers.Accept.ParseAdd("application/json");
            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Invalid($"VNPAY query returned HTTP {(int)response.StatusCode}; payment remains unconfirmed.");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Invalid("Invalid VNPAY query response.");
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in doc.RootElement.EnumerateObject())
                if (!fields.TryAdd(property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? "" : property.Value.GetRawText()))
                    return Invalid("Duplicate VNPAY query response field.");
            var signed = string.Join("|", new[] { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message",
                "vnp_TmnCode", "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo",
                "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo", "vnp_PromotionCode", "vnp_PromotionAmount" }.Select(k => Get(fields, k)));
            if (!SignatureMatches(signed, Get(fields, "vnp_SecureHash")) || Get(fields, "vnp_TxnRef") != storedReference ||
                Get(fields, "vnp_Command") != "querydr" || Get(fields, "vnp_TransactionType") != "01" || Get(fields, "vnp_ResponseCode") != "00")
                return Invalid("VNPAY query response could not be authenticated or does not identify a payment result.");
            return VerifyFields(fields);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { return Invalid("VNPAY query is unavailable or invalid; payment remains unconfirmed."); }
    }

    private PaymentVerifyResult VerifyFields(IReadOnlyDictionary<string, string> data)
    {
        var reference = Get(data, "vnp_TxnRef");
        var responseCode = Get(data, "vnp_ResponseCode");
        var status = Get(data, "vnp_TransactionStatus");
        if (Get(data, "vnp_TmnCode") != _opt.TmnCode || !ParseReference(reference, out var orderId, out _) ||
            !long.TryParse(Get(data, "vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var wire) ||
            wire <= 0 || wire > 999_999_999_999L || wire % 100 != 0 ||
            (data.ContainsKey("vnp_CurrCode") && Get(data, "vnp_CurrCode") != "VND") ||
            responseCode.Length != 2 || !responseCode.All(char.IsAsciiDigit) || status.Length != 2 || !status.All(char.IsAsciiDigit))
            return Invalid("Invalid signed VNPAY payment fields.");
        var success = status == "00" && responseCode == "00";
        var transactionId = Get(data, "vnp_TransactionNo");
        if ((status == "00" && !success) || !long.TryParse(transactionId, NumberStyles.None, CultureInfo.InvariantCulture, out var transaction) ||
            transactionId.Length > 15 || (success && transaction <= 0))
            return Invalid("Invalid signed VNPAY transaction identity.");
        var final = success || status is "02" or "09";
        return new(success, orderId, transactionId, success ? null : final ? "VNPAY payment failed." : "VNPAY payment is not final.",
            true, reference, wire / 100m, "VND", final);
    }

    private static bool ParseReference(string? reference, out int orderId, out DateTimeOffset created)
    {
        orderId = 0; created = default;
        var parts = reference?.Split('x');
        return parts is { Length: 3 } && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out orderId) && orderId > 0 &&
            DateTimeOffset.TryParseExact(parts[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out created) &&
            Guid.TryParseExact(parts[2], "N", out _);
    }
    private static string FormatDate(DateTimeOffset time) => time.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    private static string Get(IReadOnlyDictionary<string, string> data, string key) => data.TryGetValue(key, out var value) ? value : "";
    private static PaymentVerifyResult Invalid(string message) => new(false, 0, "", message);
    private static string Canonical(IReadOnlyDictionary<string, string> data) => string.Join("&", data
        .Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal) && x.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && !string.IsNullOrEmpty(x.Value))
        .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value)));
    private string Sign(string data) => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(_opt.HashSecret), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    private bool SignatureMatches(string raw, string supplied)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(raw)), Convert.FromHexString(supplied)); }
        catch (FormatException) { return false; }
    }
}
