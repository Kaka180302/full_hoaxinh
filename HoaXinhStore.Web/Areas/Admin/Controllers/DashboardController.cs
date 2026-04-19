using HoaXinhStore.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int days = 30)
    {
        var windowDays = days is 7 or 30 or 90 ? days : 30;
        var now = DateTime.UtcNow;
        var fromWindow = now.AddDays(-windowDays);
        var from7 = now.AddDays(-7);
        ViewBag.WindowDays = windowDays;

        ViewBag.ProductCount = await db.Products.CountAsync();
        ViewBag.CategoryCount = await db.Categories.CountAsync();
        ViewBag.OrderCount = await db.Orders.CountAsync();
        ViewBag.PendingOrderCount = await db.Orders.CountAsync(o => o.OrderStatus == "PendingConfirm");
        ViewBag.UnpaidOrderCount = await db.Orders.CountAsync(o => o.PaymentStatus == "Pending" || o.PaymentStatus == "AwaitingGateway");
        ViewBag.FailedPaymentCount = await db.Orders.CountAsync(o => o.PaymentStatus == "Failed" || o.PaymentStatus == "Cancelled");
        ViewBag.TodayOrderCount = await db.Orders.CountAsync(o => o.CreatedAtUtc >= now.Date);
        ViewBag.NewCustomers7Days = await db.Customers.CountAsync(c => c.CreatedAtUtc >= from7);
        ViewBag.Revenue30Days = await db.Orders.Where(o => o.CreatedAtUtc >= fromWindow && o.PaymentStatus == "Paid").SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        ViewBag.Completed30Days = await db.Orders.CountAsync(o => o.CreatedAtUtc >= fromWindow && o.OrderStatus == "Completed");
        ViewBag.Aov30Days = await db.Orders.Where(o => o.CreatedAtUtc >= fromWindow && o.PaymentStatus == "Paid").AverageAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        ViewBag.TopProducts = await db.OrderItems
            .AsNoTracking()
            .Where(i => i.Order != null && i.Order.CreatedAtUtc >= fromWindow)
            .GroupBy(i => new { i.ProductId, i.ProductNameSnapshot })
            .Select(g => new { g.Key.ProductId, g.Key.ProductNameSnapshot, Qty = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal) })
            .OrderByDescending(x => x.Qty)
            .Take(8)
            .ToListAsync();

        var lowStockVariants = await db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
                .ThenInclude(p => p.Category)
            .Where(v => v.IsActive && v.Product != null && v.Product.IsActive && v.AvailableStock < 10)
            .OrderBy(v => v.AvailableStock)
            .Take(10)
            .ToListAsync();
        ViewBag.LowStockCount = lowStockVariants.Count;
        ViewBag.LowStockVariants = lowStockVariants;

        ViewBag.RecentAttentionOrders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.OrderStatus == "PendingConfirm"
                || o.PaymentStatus == "Pending"
                || o.PaymentStatus == "AwaitingGateway"
                || o.PaymentStatus == "Failed"
                || o.PaymentStatus == "Cancelled")
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(10)
            .ToListAsync();

        var ordersWindow = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAtUtc >= fromWindow)
            .Select(o => new
            {
                o.CreatedAtUtc,
                o.OrderStatus,
                o.PaymentStatus,
                o.PaymentMethod,
                o.Note
            })
            .ToListAsync();

        ViewBag.SlaPendingTooLong = ordersWindow.Count(o => o.OrderStatus == "PendingConfirm" && o.CreatedAtUtc <= now.AddMinutes(-15));
        ViewBag.SlaProcessingTooLong = ordersWindow.Count(o =>
            (o.OrderStatus == "Confirmed" || o.OrderStatus == "Preparing") && o.CreatedAtUtc <= now.AddHours(-2));
        ViewBag.SlaPaymentTooLong = ordersWindow.Count(o =>
            (o.PaymentStatus == "Pending" || o.PaymentStatus == "AwaitingGateway") && o.CreatedAtUtc <= now.AddMinutes(-30));

        ViewBag.GhnWaitingCreate = ordersWindow.Count(o =>
            (o.OrderStatus == "Confirmed" || o.OrderStatus == "Preparing") && !HasTrackingCode(o.Note));
        ViewBag.GhnShipping = ordersWindow.Count(o => o.OrderStatus == "Shipping");
        ViewBag.GhnFailed = ordersWindow.Count(o => o.OrderStatus == "DeliveryFailed");
        ViewBag.GhnReturned = ordersWindow.Count(o => o.OrderStatus == "Returned");

        var paidByMethod = ordersWindow
            .Where(o => o.PaymentStatus == "Paid")
            .GroupBy(o => o.PaymentMethod.ToString())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var totalByMethod = ordersWindow
            .GroupBy(o => o.PaymentMethod.ToString())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        int GetCount(Dictionary<string, int> src, string key) => src.TryGetValue(key, out var value) ? value : 0;
        var codTotal = GetCount(totalByMethod, "COD");
        var vnpayTotal = GetCount(totalByMethod, "VNPAY");
        var qrTotal = GetCount(totalByMethod, "QRPAY") + GetCount(totalByMethod, "AUTOQR");
        var codPaid = GetCount(paidByMethod, "COD");
        var vnpayPaid = GetCount(paidByMethod, "VNPAY");
        var qrPaid = GetCount(paidByMethod, "QRPAY") + GetCount(paidByMethod, "AUTOQR");

        var codRate = codTotal == 0 ? 0 : Math.Round(codPaid * 100m / codTotal, 1);
        var vnpayRate = vnpayTotal == 0 ? 0 : Math.Round(vnpayPaid * 100m / vnpayTotal, 1);
        var qrRate = qrTotal == 0 ? 0 : Math.Round(qrPaid * 100m / qrTotal, 1);

        ViewBag.CodSuccessRate = codRate;
        ViewBag.VnpaySuccessRate = vnpayRate;
        ViewBag.QrSuccessRate = qrRate;
        ViewBag.CodPaidCount = codPaid;
        ViewBag.VnpayPaidCount = vnpayPaid;
        ViewBag.QrPaidCount = qrPaid;

        var dailyOrders = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAtUtc >= fromWindow)
            .Select(o => new
            {
                Day = o.CreatedAtUtc.Date,
                o.TotalAmount,
                o.PaymentStatus,
                o.OrderStatus
            })
            .ToListAsync();

        var axisDays = Enumerable.Range(0, windowDays).Select(offset => fromWindow.Date.AddDays(offset)).ToList();
        var revenueSeries = axisDays
            .Select(day => dailyOrders.Where(o => o.Day == day && o.PaymentStatus == "Paid").Sum(o => o.TotalAmount))
            .ToList();
        var orderCountSeries = axisDays
            .Select(day => dailyOrders.Count(o => o.Day == day))
            .ToList();

        var statusGroups = dailyOrders
            .GroupBy(o => new { o.Day, o.OrderStatus })
            .ToDictionary(g => (g.Key.Day, g.Key.OrderStatus), g => g.Count());

        List<int> BuildStatusSeries(string status) => axisDays
            .Select(day => statusGroups.TryGetValue((day, status), out var count) ? count : 0)
            .ToList();

        ViewBag.DashboardChartJson = JsonSerializer.Serialize(new
        {
            labels = axisDays.Select(d => d.ToString("dd/MM", CultureInfo.InvariantCulture)).ToList(),
            revenue = revenueSeries,
            orderCount = orderCountSeries,
            statusPending = BuildStatusSeries("PendingConfirm"),
            statusShipping = BuildStatusSeries("Shipping"),
            statusCompleted = BuildStatusSeries("Completed"),
            statusCancelled = BuildStatusSeries("Cancelled"),
            paymentLabels = new[] { "COD", "VNPAY", "QR" },
            paymentSuccessRate = new[] { codRate, vnpayRate, qrRate }
        });

        return View();
    }

    private static bool HasTrackingCode(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return false;
        return note.Contains("Mã vận đơn:", StringComparison.OrdinalIgnoreCase);
    }
}
