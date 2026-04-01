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
        ViewBag.ProductCount = await db.Products.CountAsync();
        ViewBag.CategoryCount = await db.Categories.CountAsync();
        ViewBag.OrderCount = await db.Orders.CountAsync();
        ViewBag.PendingOrderCount = await db.Orders.CountAsync(o => o.OrderStatus == "PendingConfirm");
        return View();
    }
}
