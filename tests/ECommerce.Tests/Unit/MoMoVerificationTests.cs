using System.Security.Cryptography;
using System.Text;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Unit;

public class MoMoVerificationTests
{
    [Theory]
    [InlineData("VND")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void Non_usd_catalog_currency_is_rejected_even_with_an_fx_rate(string currency)
    {
        var provider = new MoMoProvider(Options.Create(new PaymentOptions { MoMo = new()
        {
            PartnerCode = "test", AccessKey = "test", SecretKey = "test",
            CatalogCurrency = currency, CatalogToVndRate = 25000m
        } }));

        Assert.NotNull(provider.Quote(new(1, 1m, "", "")).Error);
    }

    [Fact]
    public void Usd_catalog_requires_explicit_rate_and_does_not_fall_back_to_legacy_rate()
    {
        var provider = new MoMoProvider(Options.Create(new PaymentOptions { MoMo = new()
        {
            PartnerCode = "test", AccessKey = "test", SecretKey = "test",
            CatalogCurrency = "USD", UsdToVndRate = 25000m
        } }));

        Assert.NotNull(provider.Quote(new(1, 10m, "", "")).Error);
    }

    [Fact]
    public void Usd_catalog_quote_uses_explicit_vnd_per_usd_rate_and_rounds_to_whole_vnd()
    {
        var provider = new MoMoProvider(Options.Create(new PaymentOptions { MoMo = new()
        {
            PartnerCode = "test", AccessKey = "test", SecretKey = "test",
            CatalogCurrency = "USD", CatalogToVndRate = 25000.5m
        } }));

        var quote = provider.Quote(new(1, 1.01m, "", ""));

        Assert.Null(quote.Error);
        Assert.Equal(25251m, quote.Amount);
        Assert.Equal("VND", quote.Currency);
    }
    [Fact]
    public async Task Disabled_provider_does_not_accept_demo_callbacks()
    {
        var p = new MoMoProvider(Options.Create(new PaymentOptions { AllowDemo = true }));
        Assert.False((await p.VerifyAsync(new Dictionary<string,string> { ["orderId"] = "1" })).Authenticated);
        Assert.NotNull(p.Quote(new(1,100,"","")).Error);
        Assert.False((await p.CreatePaymentAsync(new(1,100,"",""))).Completed);
    }

    [Fact]
    public async Task Signed_notification_binds_amount_reference_partner_and_transaction()
    {
        var opts = new PaymentOptions { MoMo = new() { PartnerCode = "test-partner", AccessKey = "test-access", SecretKey = "test-secret" } };
        var p = new MoMoProvider(Options.Create(opts));
        var d = new Dictionary<string,string> { ["orderId"]="42-attempt",["requestId"]="42-attempt",["amount"]="1000",["resultCode"]="0",["transId"]="123",["partnerCode"]="test-partner" };
        void Sign()
        {
            string G(string k)=>d.GetValueOrDefault(k, "");
            var raw=$"accessKey=test-access&amount={G("amount")}&extraData={G("extraData")}&message={G("message")}&orderId={G("orderId")}&orderInfo={G("orderInfo")}&orderType={G("orderType")}&partnerCode={G("partnerCode")}&payType={G("payType")}&requestId={G("requestId")}&responseTime={G("responseTime")}&resultCode={G("resultCode")}&transId={G("transId")}";
            d["signature"]=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(raw)));
        }
        Sign();
        var verified = await p.VerifyAsync(d);
        Assert.True(verified.Authenticated); Assert.True(verified.Success);
        Assert.Equal(42, verified.OrderId); Assert.Equal(1000m, verified.Amount); Assert.Equal("VND", verified.Currency);
        d["amount"]="2000";
        Assert.False((await p.VerifyAsync(d)).Authenticated);
        d["partnerCode"]="wrong-partner"; Sign();
        Assert.False((await p.VerifyAsync(d)).Authenticated);
    }
}
