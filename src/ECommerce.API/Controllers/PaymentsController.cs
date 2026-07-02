using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly IConfiguration _config;

    public PaymentsController(IPaymentService payments, IConfiguration config)
    {
        _payments = payments;
        _config = config;
    }

    // VNPay return URL — cổng redirect khách về kèm query params đã ký.
    [AllowAnonymous]
    [HttpGet("vnpay/callback")]
    public async Task<IActionResult> VnPayCallback(CancellationToken ct)
    {
        var data = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _payments.ConfirmAsync("vnpay", data, ct);
        return RedirectToClient(result.Success);
    }

    // Stripe success URL — kèm session_id.
    [AllowAnonymous]
    [HttpGet("stripe/callback")]
    public async Task<IActionResult> StripeCallback(CancellationToken ct)
    {
        var data = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _payments.ConfirmAsync("stripe", data, ct);
        return RedirectToClient(result.Success);
    }

    private IActionResult RedirectToClient(bool success)
    {
        var baseUrl = _config["Client:BaseUrl"] ?? "http://localhost:5173";
        var path = success ? "/orders?payment=success" : "/orders?payment=failed";
        return Redirect($"{baseUrl}{path}");
    }
}
