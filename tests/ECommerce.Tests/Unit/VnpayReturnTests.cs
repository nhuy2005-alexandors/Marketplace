using ECommerce.API.Controllers;
using ECommerce.Infrastructure.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public class VnpayReturnTests
{
    [Fact]
    public async Task Signed_browser_return_never_calls_payment_mutation_and_duplicate_keys_are_rejected()
    {
        var provider = new VnpayProvider(Options.Create(VnpayProviderTests.Settings()));
        var controller = new VnpayPaymentsController(null!, new PaymentProviderFactory([provider]), new ConfigurationBuilder().Build())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var data = VnpayProviderTests.Callback(); VnpayProviderTests.Sign(data);
        controller.Request.QueryString = QueryString.Create(data.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)));
        Assert.EndsWith("payment=pending&orderId=42", Assert.IsType<RedirectResult>(await controller.Return(default)).Url);
        controller.Request.QueryString = new QueryString("?vnp_TxnRef=one&vnp_TxnRef=two");
        Assert.EndsWith("payment=failed", Assert.IsType<RedirectResult>(await controller.Return(default)).Url);
        var ipn = Assert.IsType<OkObjectResult>(await controller.Ipn(default));
        Assert.Contains("97", System.Text.Json.JsonSerializer.Serialize(ipn.Value));
    }
}
