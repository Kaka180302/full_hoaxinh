using HoaXinhStore.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var from30 = now.AddDays(-30);
        ViewBag.ProductCount = await db.Products.CountAsync();
        ViewBag.CategoryCount = await db.Categories.CountAsync();
        ViewBag.OrderCount = await db.Orders.CountAsync();
        ViewBag.PendingOrderCount = await db.Orders.CountAsync(o => o.OrderStatus == "PendingConfirm");
        ViewBag.Revenue30Days = await db.Orders.Where(o => o.CreatedAtUtc >= from30 && o.PaymentStatus == "Paid").SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        ViewBag.Completed30Days = await db.Orders.CountAsync(o => o.CreatedAtUtc >= from30 && o.OrderStatus == "Completed");
        ViewBag.Aov30Days = await db.Orders.Where(o => o.CreatedAtUtc >= from30 && o.PaymentStatus == "Paid").AverageAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        ViewBag.TopProducts = await db.OrderItems
            .AsNoTracking()
            .Where(i => i.Order != null && i.Order.CreatedAtUtc >= from30)
            .GroupBy(i => new { i.ProductId, i.ProductNameSnapshot })
            .Select(g => new { g.Key.ProductId, g.Key.ProductNameSnapshot, Qty = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal) })
            .OrderByDescending(x => x.Qty)
            .Take(8)
            .ToListAsync();
        var lowStockProducts = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.StockQuantity < 10 && p.IsActive)
            .OrderBy(p => p.StockQuantity)
            .Take(10)
            .ToListAsync();
        ViewBag.LowStockCount = lowStockProducts.Count;
        ViewBag.LowStockProducts = lowStockProducts;
        return View();
    }
}
