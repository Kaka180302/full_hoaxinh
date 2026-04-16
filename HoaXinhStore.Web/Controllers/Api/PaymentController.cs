using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Services.Inventory;
using HoaXinhStore.Web.Services.Notifications;
using HoaXinhStore.Web.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Controllers.Api;

[ApiController]
[Route("api/payment")]
public class PaymentController(
    AppDbContext db,
    IVnpayService vnpayService,
    IEmailService emailService,
    IInventoryService inventoryService) : ControllerBase
{
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> VnpayIpn()
    {
        var txnRef = Request.Query["vnp_TxnRef"].ToString();
        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        var transactionStatus = Request.Query["vnp_TransactionStatus"].ToString();
        var amountRaw = Request.Query["vnp_Amount"].ToString();

        if (!vnpayService.IsValidSignature(Request.Query))
        {
            return Ok(new { RspCode = "97", Message = "Invalid signature" });
        }

        var order = await db.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderNo == txnRef);

        if (order is null)
        {
            return Ok(new { RspCode = "01", Message = "Order not found" });
        }

        var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is null)
        {
            return Ok(new { RspCode = "01", Message = "Payment not found" });
        }

        if (!long.TryParse(amountRaw, out var amountPaid) || amountPaid != (long)Math.Round(order.TotalAmount * 100m))
        {
            return Ok(new { RspCode = "04", Message = "Invalid amount" });
        }

        var wasPaid = payment.Status == "Paid";
        var wasTerminalFailure = payment.Status == "Cancelled" || payment.Status == "Failed";

        payment.RawResponseJson = string.Join("&", Request.Query.Select(kv => $"{kv.Key}={kv.Value}"));
        payment.Provider = "VNPAY";
        payment.PaymentMethod = PaymentMethod.VNPAY.ToString();
        payment.TransactionRef = txnRef;

        if (responseCode == "00" && transactionStatus == "00")
        {
            payment.Status = "Paid";
            payment.PaidAtUtc ??= DateTime.UtcNow;
            order.PaymentStatus = "Paid";
            if (!string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                await inventoryService.ConsumeOrderReservationsAsync(order);
            }
            order.OrderStatus = "Confirmed";

            if (!wasPaid)
            {
                var trackUrl = Url.Action(
                    "TrackOrder",
                    "Store",
                    new { orderNo = order.OrderNo, phoneNumber = order.Customer?.Phone ?? string.Empty },
                    Request.Scheme,
                    Request.Host.Value);
                await emailService.SendOrderPaymentSuccessAsync(order, trackUrl);
            }
        }
        else
        {
            if (!wasPaid && !wasTerminalFailure)
            {
                await inventoryService.ReleaseOrderReservationsAsync(order);
            }
            payment.Status = responseCode == "24" ? "Cancelled" : "Failed";
            order.PaymentStatus = responseCode == "24" ? "Cancelled" : "Failed";
        }

        await db.SaveChangesAsync();
        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }
}
