namespace ECommerce.Infrastructure.Payments;

public class PaymentOptions
{
    // Cho phép chế độ demo (hoàn tất ngay không cần key thật). Chỉ bật ở Development.
    public bool AllowDemo { get; set; }
    public VnPayOptions VnPay { get; set; } = new();
    public StripeOptions Stripe { get; set; } = new();
}

public class VnPayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpc/Pay.html";
    public string Version { get; set; } = "2.1.0";
    public bool Enabled => !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret);
}

public class StripeOptions
{
    public string SecretKey { get; set; } = "";
    public string SuccessUrl { get; set; } = "http://localhost:5173/orders";
    public string CancelUrl { get; set; } = "http://localhost:5173/cart";
    public bool Enabled => !string.IsNullOrWhiteSpace(SecretKey);
}
