using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CategoriesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var items = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync();
        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new CategoryEditViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null) return NotFound();

        return View(new CategoryEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            SkuPrefix = entity.SkuPrefix,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CategoryEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", vm);
        }

        Category entity;
        if (vm.Id is null)
        {
            entity = new Category();
            db.Categories.Add(entity);
        }
        else
        {
            entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == vm.Id.Value) ?? throw new InvalidOperationException("Category not found");
        }

        entity.Name = vm.Name.Trim();
        entity.Slug = vm.Slug.Trim();
        entity.SkuPrefix = (vm.SkuPrefix ?? string.Empty).Trim().ToUpperInvariant();
        entity.IsActive = vm.IsActive;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
