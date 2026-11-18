namespace ECommerce.Infrastructure.Payments;

public class PaymentOptions
{
    // Cho phép chế độ demo (hoàn tất ngay không cần key thật). Chỉ bật ở Development.
    public bool AllowDemo { get; set; }
    public MoMoOptions MoMo { get; set; } = new();
    public VnpayOptions Vnpay { get; set; } = new();
}

public class VnpayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string ReturnUrl { get; set; } = "http://localhost:5215/api/payments/vnpay/return";
    public string CatalogCurrency { get; set; } = "USD";
    public decimal? CatalogToVndRate { get; set; }
    public string QueryIpAddress { get; set; } = "127.0.0.1";
    public bool Enabled => !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret);
}

public class MoMoOptions
{
    public string PartnerCode { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";
    public string RedirectUrl { get; set; } = "http://localhost:5215/api/payments/momo/callback";
    public string IpnUrl { get; set; } = "http://localhost:5215/api/payments/momo/ipn";
    // Legacy key retained for configuration compatibility only; never used for payment quotes.
    public decimal UsdToVndRate { get; set; } = 25000m;
    // Marketplace catalog amounts are always USD. MoMo requires an explicit VND-per-USD rate.
    public string CatalogCurrency { get; set; } = "USD";
    public decimal? CatalogToVndRate { get; set; }
    public bool Enabled => !string.IsNullOrWhiteSpace(PartnerCode)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}
