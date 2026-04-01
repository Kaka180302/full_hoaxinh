using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Controllers;

public class StoreController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new StoreCategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = string.IsNullOrWhiteSpace(c.Slug) ? "all" : c.Slug
            })
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null)
            .OrderBy(p => p.Id)
            .Select(p => new StoreProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug
            })
            .ToListAsync();

        var model = new StoreIndexViewModel
        {
            Categories = categories,
            Products = products
        };

        return View(model);
    }
}
