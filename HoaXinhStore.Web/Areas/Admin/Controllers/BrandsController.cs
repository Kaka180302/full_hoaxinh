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
public class BrandsController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    public async Task<IActionResult> Index(string q = "")
    {
        var query = db.CategoryBrands
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => x.Name.Contains(q));
        }

        ViewBag.Query = q;
        var items = await query.OrderBy(x => x.Name).ToListAsync();
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new BrandEditViewModel();
        await LoadCategoryOptions(vm);
        return View("Edit", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.CategoryBrands.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();
        var vm = new BrandEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            CategoryId = entity.CategoryId,
            IsActive = entity.IsActive,
            ImageUrl = entity.ImageUrl
        };
        await LoadCategoryOptions(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BrandEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoryOptions(vm);
            return View("Edit", vm);
        }

        CategoryBrand entity;
        if (vm.Id.HasValue)
        {
            entity = await db.CategoryBrands.FirstOrDefaultAsync(x => x.Id == vm.Id.Value) ?? throw new InvalidOperationException("Brand not found");
        }
        else
        {
            entity = new CategoryBrand();
            db.CategoryBrands.Add(entity);
        }

        entity.Name = vm.Name.Trim();
        entity.CategoryId = vm.CategoryId;
        entity.IsActive = vm.IsActive;
        if (vm.ImageFile is { Length: > 0 })
        {
            entity.ImageUrl = await SaveImageAsync(vm.ImageFile);
        }
        else if (!vm.Id.HasValue && string.IsNullOrWhiteSpace(entity.ImageUrl))
        {
            entity.ImageUrl = string.Empty;
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var entity = await db.CategoryBrands.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();
        entity.IsActive = !entity.IsActive;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoryOptions(BrandEditViewModel vm)
    {
        vm.CategoryOptions = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
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
        var uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "brands");
        Directory.CreateDirectory(uploadsRoot);
        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsRoot, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);
        return $"/uploads/brands/{fileName}";
    }
}

