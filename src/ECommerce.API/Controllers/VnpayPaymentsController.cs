using ECommerce.Application.Common;
using System.Text.Json.Serialization;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Route("api/payments/vnpay")]
public class VnpayPaymentsController(IPaymentService payments, IPaymentProviderFactory providers, IConfiguration config) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("return")]
    public async Task<IActionResult> Return(CancellationToken ct)
    {
        var data = ReadQuery();
        var verified = data is null ? null : await providers.Resolve("vnpay").VerifyAsync(data, ct);
        // A browser return never commits payment. IPN or a server query must confirm it.
        var outcome = verified?.Authenticated == true ? "pending" : "failed";
        var order = verified?.Authenticated == true ? $"&orderId={verified.OrderId}" : "";
        return Redirect($"{config["Client:BaseUrl"] ?? "http://localhost:5173"}/orders?payment={outcome}{order}");
    }

    [AllowAnonymous]
    [HttpGet("ipn")]
    public async Task<IActionResult> Ipn(CancellationToken ct)
    {
        var data = ReadQuery();
        if (data is null) return Ok(new IpnResponse("97", "Invalid callback parameters"));
        var result = await payments.ConfirmAsync("vnpay", data, ct);
        // A committed reconciliation observation is durable; do not ask the gateway to retry it.
        return result.Success
            ? Ok(new IpnResponse("00", result.Value?.Payment?.RequiresReconciliation == true ? "Recorded for reconciliation" : "Confirm Success"))
            : Ok(new IpnResponse(result.ErrorType == ErrorType.NotFound ? "01" : result.ErrorType == ErrorType.Validation ? "97" : "99", "Payment was not confirmed"));
    }

    [Authorize]
    [HttpPost("orders/{orderId:int}/query")]
    public async Task<ActionResult<OrderDto>> Query(int orderId, CancellationToken ct)
        => ToResponse(await payments.QueryAsync(UserId, IsAdmin, orderId, ct));

    private Dictionary<string, string>? ReadQuery()
        => Request.Query.Any(x => x.Value.Count != 1)
            ? null
            : Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);

    private record IpnResponse(
        [property: JsonPropertyName("RspCode")] string Code,
        [property: JsonPropertyName("Message")] string Message);
}
