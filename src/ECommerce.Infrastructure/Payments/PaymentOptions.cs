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
    // Tỷ giá quy đổi USD -> VND cho demo (MoMo yêu cầu VND nguyên).
    public decimal UsdToVndRate { get; set; } = 25000m;
    public bool Enabled => !string.IsNullOrWhiteSpace(PartnerCode)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}
