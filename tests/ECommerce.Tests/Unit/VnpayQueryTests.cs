using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public class VnpayQueryTests
{
    private sealed class Gateway(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }
    private static string Hmac(string raw) => Convert.ToHexString(HMACSHA512.HashData(
        Encoding.UTF8.GetBytes("fixture-only-secret"), Encoding.UTF8.GetBytes(raw)));

    [Theory]
    [InlineData("00", false, true, true)]
    [InlineData("01", false, true, false)]
    [InlineData("02", false, true, true)]
    [InlineData("00", true, false, true)]
    public async Task Query_uses_its_own_signature_and_rejects_tampering_or_pending_results(
        string status, bool tamper, bool authenticated, bool final)
    {
        var handler = new Gateway(async request =>
        {
            Assert.Contains(request.Headers.UserAgent, x => x.Product?.Name == "Marketplace-Sandbox");
            Assert.Equal("sandbox.vnpayment.vn", request.RequestUri!.Host);
            Assert.Equal("/merchant_webapi/api/transaction", request.RequestUri.AbsolutePath);
            var sent = JsonSerializer.Deserialize<Dictionary<string, string>>(await request.Content!.ReadAsStringAsync())!;
            Assert.Equal("querydr", sent["vnp_Command"]);
            Assert.Equal("20261115190000", sent["vnp_TransactionDate"]);
            Assert.Equal(VnpayProviderTests.Reference, sent["vnp_TxnRef"]);
            Assert.Equal(Hmac(string.Join("|", new[] { "vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode",
                "vnp_TxnRef", "vnp_TransactionDate", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo" }.Select(k => sent[k]))), sent["vnp_SecureHash"], ignoreCase: true);
            var data = new Dictionary<string, string>
            {
                ["vnp_ResponseId"] = "query-fixture", ["vnp_Command"] = "querydr", ["vnp_ResponseCode"] = "00",
                ["vnp_Message"] = "Success", ["vnp_TmnCode"] = "TESTVNP1", ["vnp_TxnRef"] = VnpayProviderTests.Reference,
                ["vnp_Amount"] = "2563700", ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261115190200",
                ["vnp_TransactionNo"] = status == "00" ? "12345" : "0", ["vnp_TransactionType"] = "01",
                ["vnp_TransactionStatus"] = status, ["vnp_OrderInfo"] = "Order42", ["vnp_PromotionCode"] = "", ["vnp_PromotionAmount"] = ""
            };
            data["vnp_SecureHash"] = Hmac(string.Join("|", data.Values));
            if (tamper) data["vnp_Amount"] = "2563800";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data)) };
        });
        using var http = new HttpClient(handler);
        var result = await new VnpayProvider(Options.Create(VnpayProviderTests.Settings()), http).QueryAsync(VnpayProviderTests.Reference);
        Assert.Equal(authenticated, result.Authenticated);
        if (authenticated)
        {
            Assert.Equal(final, result.IsFinal);
            Assert.Equal(status == "00", result.Success);
        }
    }

    [Fact]
    public async Task Timeout_does_not_report_a_failed_payment_or_authenticated_result()
    {
        using var http = new HttpClient(new Gateway(_ => throw new TaskCanceledException()));
        var result = await new VnpayProvider(Options.Create(VnpayProviderTests.Settings()), http).QueryAsync(VnpayProviderTests.Reference);
        Assert.False(result.Authenticated); Assert.False(result.Success);
        Assert.Contains("unconfirmed", result.Error);
    }
}
