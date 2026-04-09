using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class OrdersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(
        string q = "",
        string? orderStatus = null,
        string? paymentStatus = null,
        int pendingPage = 1,
        int completedPage = 1,
        string activeTab = "pending")
    {
        const int pageSize = 10;
        pendingPage = Math.Max(1, pendingPage);
        completedPage = Math.Max(1, completedPage);

        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(o =>
                o.OrderNo.Contains(keyword) ||
                (o.Customer != null && (
                    o.Customer.FullName.Contains(keyword) ||
                    o.Customer.Phone.Contains(keyword) ||
                    o.Customer.Email.Contains(keyword))));
        }

        if (!string.IsNullOrWhiteSpace(orderStatus))
        {
            query = query.Where(o => o.OrderStatus == orderStatus);
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus);
        }


        var pendingQuery = query
            .Where(o => o.OrderStatus != "Completed")
            .OrderByDescending(o => o.CreatedAtUtc);
        var completedQuery = query
            .Where(o => o.OrderStatus == "Completed")
            .OrderByDescending(o => o.CreatedAtUtc);

        var pendingTotal = await pendingQuery.CountAsync();
        var completedTotal = await completedQuery.CountAsync();
        var pendingTotalPages = Math.Max(1, (int)Math.Ceiling(pendingTotal / (double)pageSize));
        var completedTotalPages = Math.Max(1, (int)Math.Ceiling(completedTotal / (double)pageSize));
        pendingPage = Math.Min(pendingPage, pendingTotalPages);
        completedPage = Math.Min(completedPage, completedTotalPages);

        var pendingItems = await pendingQuery
            .Skip((pendingPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var completedItems = await completedQuery
            .Skip((completedPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = await query
            .ToListAsync();

        ViewBag.PendingOrders = pendingItems;
        ViewBag.CompletedOrders = completedItems;
        ViewBag.PendingTotal = pendingTotal;
        ViewBag.CompletedTotal = completedTotal;
        ViewBag.PendingPage = pendingPage;
        ViewBag.CompletedPage = completedPage;
        ViewBag.PendingTotalPages = pendingTotalPages;
        ViewBag.CompletedTotalPages = completedTotalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.ActiveTab = string.Equals(activeTab, "completed", StringComparison.OrdinalIgnoreCase) ? "completed" : "pending";
        ViewBag.Query = q;
        ViewBag.SelectedOrderStatus = orderStatus ?? string.Empty;
        ViewBag.SelectedPaymentStatus = paymentStatus ?? string.Empty;
        ViewBag.OrderStatusOptions = GetOrderStatusOptions();
        ViewBag.PaymentStatusOptions = GetPaymentStatusOptions();
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.Timelines)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();
        ViewBag.OrderStatusOptions = GetOrderStatusOptions();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string orderStatus, string? shippingCarrier, string? trackingCode, string? note)
    {
        var order = await db.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();

        var normalizedStatus = (orderStatus ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedStatus))
        {
            TempData["OrderAdminMessage"] = "Trạng thái đơn hàng không hợp lệ.";
            TempData["OrderAdminStatus"] = "error";
            return RedirectToAction(nameof(Details), new { id });
        }

        var fromStatus = order.OrderStatus;
        order.OrderStatus = normalizedStatus;
        order.Note = BuildShippingNote(shippingCarrier, trackingCode, note);
        db.OrderTimelines.Add(new OrderTimeline
        {
            OrderId = order.Id,
            Action = "StatusChanged",
            FromStatus = fromStatus,
            ToStatus = normalizedStatus,
            Note = note ?? string.Empty
        });

        if (IsCompletedStatus(order.OrderStatus) && order.PaymentMethod == PaymentMethod.COD && order.PaymentStatus != "Paid")
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
        TempData["OrderAdminMessage"] = "Cập nhật trạng thái đơn hàng thành công.";
        TempData["OrderAdminStatus"] = "success";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static bool IsCompletedStatus(string? status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static List<(string Value, string Label)> GetOrderStatusOptions()
    {
        return
        [
            ("PendingConfirm", "Chờ xác nhận"),
            ("Confirmed", "Đã xác nhận"),
            ("Preparing", "Đang chuẩn bị hàng"),
            ("Shipping", "Đang giao hàng"),
            ("Completed", "Hoàn thành"),
            ("Cancelled", "Đã hủy"),
            ("DeliveryFailed", "Giao thất bại"),
            ("Returned", "Hoàn hàng")
        ];
    }

    private static List<(string Value, string Label)> GetPaymentStatusOptions()
    {
        return
        [
            ("Pending", "Chưa thanh toán"),
            ("AwaitingGateway", "Chờ cổng thanh toán"),
            ("Paid", "Đã thanh toán"),
            ("Failed", "Thanh toán thất bại"),
            ("Cancelled", "Đã hủy thanh toán")
        ];
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
