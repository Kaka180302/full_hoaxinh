using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HoaXinhStore.Web.Controllers.Api;

[ApiController]
[Route("api/shipping")]
public class ShippingWebhookController(
    AppDbContext db,
    IOptions<ShippingIntegrationOptions> shippingOptions) : ControllerBase
{
    private readonly ShippingIntegrationOptions _shipping = shippingOptions.Value;

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] ShippingWebhookRequest request)
    {
        var providedKey = Request.Headers["X-Shipping-Key"].ToString();
        if (string.IsNullOrWhiteSpace(_shipping.WebhookKey) ||
            !string.Equals(providedKey, _shipping.WebhookKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Invalid webhook key" });
        }

        if (string.IsNullOrWhiteSpace(request.OrderNo))
        {
            return BadRequest(new { message = "OrderNo is required" });
        }

        var order = await db.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderNo == request.OrderNo);

        if (order is null)
        {
            return NotFound(new { message = "Order not found" });
        }

        order.OrderStatus = MapShippingStatus(request.Status, order.OrderStatus);
        order.Note = BuildShippingNote(request.Carrier, request.TrackingCode, request.Note);

        if (string.Equals(order.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(order.PaymentMethod.ToString(), "COD", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            order.PaymentStatus = "Paid";
            var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
            if (payment is not null)
            {
                payment.Status = "Paid";
                payment.PaidAtUtc ??= DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Updated", orderStatus = order.OrderStatus, paymentStatus = order.PaymentStatus });
    }

    private static string MapShippingStatus(string? externalStatus, string currentStatus)
    {
        var status = (externalStatus ?? string.Empty).Trim().ToLowerInvariant();
        return status switch
        {
            "pending" => "Confirmed",
            "picked" => "Preparing",
            "shipping" => "Shipping",
            "in_transit" => "Shipping",
            "delivered" => "Completed",
            "failed" => "DeliveryFailed",
            "cancelled" => "Cancelled",
            "returned" => "Returned",
            _ => currentStatus
        };
    }

    private static string BuildShippingNote(string? carrier, string? trackingCode, string? note)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(carrier))
        {
            parts.Add($"Đơn vị vận chuyển: {carrier.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(trackingCode))
        {
            parts.Add($"Mã vận đơn: {trackingCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            parts.Add($"Ghi chú: {note.Trim()}");
        }

        return string.Join(Environment.NewLine, parts);
    }
}

public class ShippingWebhookRequest
{
    public string OrderNo { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
