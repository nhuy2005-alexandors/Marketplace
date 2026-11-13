namespace ECommerce.Infrastructure.Payments;

public class PaymentOptions
{
    // Cho phép chế độ demo (hoàn tất ngay không cần key thật). Chỉ bật ở Development.
    public bool AllowDemo { get; set; }
    public MoMoOptions MoMo { get; set; } = new();
}

public class MoMoOptions
{
    public string PartnerCode { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";
    public string RedirectUrl { get; set; } = "http://localhost:5215/api/payments/momo/callback";
    public string IpnUrl { get; set; } = "http://localhost:5215/api/payments/momo/ipn";
    // Legacy configuration key, retained but ignored. No inferred USD/VND conversion.
    public decimal UsdToVndRate { get; set; } = 25000m;
    // Required for real provider initiation; legacy UsdToVndRate is not used.
    public string CatalogCurrency { get; set; } = "";
    public decimal? CatalogToVndRate { get; set; }
    public bool Enabled => !string.IsNullOrWhiteSpace(PartnerCode)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}
