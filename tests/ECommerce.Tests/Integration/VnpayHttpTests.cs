using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ECommerce.Tests.Integration;

public class VnpayHttpTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Authenticated_vnpay_request_passes_http_validation_and_reaches_order_lookup()
    {
        using var client = factory.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"vnpay-http-{Guid.NewGuid():N}@example.invalid", password = "SyntheticTest1!", fullName = "VNPAY HTTP fixture"
        });
        registration.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("token").GetString());
        using var response = await client.PostAsJsonAsync("/api/orders/999999/pay", new { method = "vnpay" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("?vnp_TxnRef=bad&vnp_SecureHash=00")]
    [InlineData("?vnp_TxnRef=one&%76np_TxnRef=two")]
    public async Task Ipn_uses_exact_gateway_property_names_and_rejects_bad_inputs(string query)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/payments/vnpay/ipn" + query);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("RspCode", out var code));
        Assert.Equal("97", code.GetString());
        Assert.True(json.RootElement.TryGetProperty("Message", out _));
    }
}
