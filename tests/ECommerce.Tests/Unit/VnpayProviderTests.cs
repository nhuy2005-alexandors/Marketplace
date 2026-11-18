using System.Net;
using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Payments;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public class VnpayProviderTests
{
    internal const string Reference = "42x20261115120000x0123456789abcdef0123456789abcdef";
    internal static PaymentOptions Settings() => new() { Vnpay = new()
    {
        TmnCode = "TESTVNP1", HashSecret = "fixture-only-secret", CatalogToVndRate = 25637m
    } };
    internal static Dictionary<string, string> Callback(string reference = Reference) => new()
    {
        ["vnp_TmnCode"] = "TESTVNP1", ["vnp_TxnRef"] = reference, ["vnp_Amount"] = "2563700",
        ["vnp_TransactionNo"] = "12345", ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00",
        ["vnp_OrderInfo"] = "Thanh toán / đơn 42 + &", ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261115190200"
    };
    internal static void Sign(Dictionary<string, string> data)
    {
        var raw = string.Join("&", data.Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal) &&
            x.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && x.Value != "")
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value)));
        data["vnp_SecureHash"] = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes("fixture-only-secret"), Encoding.UTF8.GetBytes(raw)));
    }

    [Fact]
    public void Quote_freezes_whole_vnd_and_rejects_missing_credentials_fx_and_overflow()
    {
        var config = Settings();
        var context = new PaymentContext(42, 1.01m, "", "127.0.0.1", Reference);
        Assert.Equal(new PaymentQuote(25893m, "VND", null), new VnpayProvider(Options.Create(config)).Quote(context));
        config.Vnpay.CatalogToVndRate = null;
        Assert.NotNull(new VnpayProvider(Options.Create(config)).Quote(context).Error);
        config = Settings(); config.Vnpay.HashSecret = "";
        Assert.NotNull(new VnpayProvider(Options.Create(config)).Quote(context).Error);
        Assert.NotNull(new VnpayProvider(Options.Create(Settings())).Quote(context with { Amount = decimal.MaxValue }).Error);
    }

    [Fact]
    public async Task Url_uses_sandbox_scaled_amount_frozen_time_and_server_return_url()
    {
        var result = await new VnpayProvider(Options.Create(Settings())).CreatePaymentAsync(
            new(42, 1m, "https://attacker.invalid", "127.0.0.1", Reference));
        Assert.Null(result.Error);
        var uri = new Uri(result.RedirectUrl!);
        Assert.Equal("sandbox.vnpayment.vn", uri.Host);
        var data = QueryHelpers.ParseQuery(uri.Query).ToDictionary(x => x.Key, x => x.Value.ToString());
        Assert.Equal("2563700", data["vnp_Amount"]);
        Assert.Equal("20261115190000", data["vnp_CreateDate"]);
        Assert.Equal("20261115191500", data["vnp_ExpireDate"]);
        Assert.Equal(Settings().Vnpay.ReturnUrl, data["vnp_ReturnUrl"]);
        var supplied = data["vnp_SecureHash"]; Sign(data);
        Assert.Equal(data["vnp_SecureHash"], supplied, ignoreCase: true);
        Assert.Equal(Reference, result.TransactionId);
        Assert.Equal(25637m, result.ProviderAmount);
    }

    [Theory]
    [InlineData("vnp_Amount", "2563800", false)]
    [InlineData("vnp_TmnCode", "OTHER123", true)]
    [InlineData("vnp_CurrCode", "USD", true)]
    [InlineData("vnp_TxnRef", "bad-reference", true)]
    [InlineData("vnp_TransactionNo", "0", true)]
    public async Task Tampered_or_invalid_signed_callback_is_rejected(string key, string value, bool resign)
    {
        var data = Callback(); Sign(data); data[key] = value; if (resign) Sign(data);
        Assert.False((await new VnpayProvider(Options.Create(Settings())).VerifyAsync(data)).Authenticated);
    }

    [Fact]
    public async Task Signed_success_and_failure_are_distinguished_and_amount_is_unscaled()
    {
        var data = Callback(); Sign(data);
        // Independent Python urllib.parse.urlencode + HMACSHA512 fixture, including UTF-8 and '+' escaping.
        Assert.Equal("42cedabf0c3fd10819856d65b1442ee3480abd803e8beb4cb68f82f7fae6b5e3ebb21fae16e1639450c87e698f8be5b34a0a6e3f434f9ef3e9eceeea6b7c0cfe",
            data["vnp_SecureHash"].ToLowerInvariant());
        var provider = new VnpayProvider(Options.Create(Settings()));
        var success = await provider.VerifyAsync(data);
        Assert.True(success.Authenticated); Assert.True(success.Success);
        Assert.Equal(42, success.OrderId); Assert.Equal(Reference, success.RequestId);
        Assert.Equal(25637m, success.Amount); Assert.Equal("VND", success.Currency);
        data["vnp_ResponseCode"] = "24"; data["vnp_TransactionStatus"] = "02"; data["vnp_TransactionNo"] = "0"; Sign(data);
        var failure = await provider.VerifyAsync(data);
        Assert.True(failure.Authenticated); Assert.False(failure.Success);
    }
}
