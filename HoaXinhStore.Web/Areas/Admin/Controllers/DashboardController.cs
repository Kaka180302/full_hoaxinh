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
        return View();
    }
}
