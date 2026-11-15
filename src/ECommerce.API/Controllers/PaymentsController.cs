using System.Text.Json;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Application.Common;

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

    // MoMo redirect URL — cổng redirect khách về kèm query params đã ký.
    [AllowAnonymous]
    [HttpGet("momo/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> MoMoCallback(CancellationToken ct)
    {
        var data = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _payments.ConfirmAsync("momo", data, ct);
        if (result.Value?.Payment?.RequiresReconciliation == true)
            return Redirect($"{_config["Client:BaseUrl"] ?? "http://localhost:5173"}/orders?payment=reconciliation");
        return RedirectToClient(result.Success && result.Value?.Payment?.Status == "Completed");
    }

    // MoMo IPN — server-to-server notification (JSON body), xác nhận độc lập với redirect.
    [AllowAnonymous]
    [HttpPost("momo/ipn")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MoMoIpn([FromBody] Dictionary<string, JsonElement> payload, CancellationToken ct)
    {
        var data = payload.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ValueKind == JsonValueKind.String ? kv.Value.GetString() ?? "" : kv.Value.GetRawText());
        var result = await _payments.ConfirmAsync("momo", data, ct);
        return result.Success ? NoContent() : StatusCode(result.ErrorType == ErrorType.Conflict ? 409 : 400,
            new { error = result.Error });
    }

    private IActionResult RedirectToClient(bool success)
    {
        var baseUrl = _config["Client:BaseUrl"] ?? "http://localhost:5173";
        var path = success ? "/orders?payment=success" : "/orders?payment=failed";
        return Redirect($"{baseUrl}{path}");
    }
}
