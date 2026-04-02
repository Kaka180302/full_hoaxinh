using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ProductsController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    public async Task<IActionResult> Index(string q = "", int? categoryId = null, int page = 1, int pageSize = 10)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.Name.Contains(q) || p.Sku.Contains(q));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Query = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.Categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new AdminProductEditViewModel();
        await LoadCategoryOptions(vm);
        return View("Edit", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.Products.FindAsync(id);
        if (entity is null) return NotFound();

        var vm = new AdminProductEditViewModel
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Price = entity.Price,
            StockQuantity = entity.StockQuantity,
            ImageUrl = entity.ImageUrl,
            Summary = entity.Summary,
            Descriptions = entity.Descriptions,
            CategoryId = entity.CategoryId,
            IsActive = entity.IsActive
        };

        await LoadCategoryOptions(vm);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var entity = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity is null) return NotFound();
        return View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminProductEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoryOptions(vm);
            return View("Edit", vm);
        }

        if (vm.ImageFile is { Length: > 0 })
        {
            vm.ImageUrl = await SaveImageAsync(vm.ImageFile);
        }

        if (vm.Id is null)
        {
            var entity = new Product
            {
                Sku = vm.Sku,
                Name = vm.Name,
                Price = vm.Price,
                StockQuantity = vm.StockQuantity,
                ImageUrl = vm.ImageUrl,
                Summary = vm.Summary,
                Descriptions = vm.Descriptions,
                CategoryId = vm.CategoryId,
                IsActive = vm.IsActive
            };
            db.Products.Add(entity);
        }
        else
        {
            var entity = await db.Products.FindAsync(vm.Id.Value);
            if (entity is null) return NotFound();

            entity.Sku = vm.Sku;
            entity.Name = vm.Name;
            entity.Price = vm.Price;
            entity.StockQuantity = vm.StockQuantity;
            entity.ImageUrl = vm.ImageUrl;
            entity.Summary = vm.Summary;
            entity.Descriptions = vm.Descriptions;
            entity.CategoryId = vm.CategoryId;
            entity.IsActive = vm.IsActive;
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var entity = await db.Products.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = !entity.IsActive;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoryOptions(AdminProductEditViewModel vm)
    {
        vm.CategoryOptions = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        var uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        return $"/uploads/products/{fileName}";
    }
}
