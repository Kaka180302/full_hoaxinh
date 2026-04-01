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
        var items = await db.Categories.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new CategoryEditViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.Categories.FindAsync(id);
        if (entity is null) return NotFound();

        return View(new CategoryEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
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

        if (vm.Id is null)
        {
            db.Categories.Add(new Category
            {
                Name = vm.Name,
                Slug = vm.Slug,
                IsActive = vm.IsActive
            });
        }
        else
        {
            var entity = await db.Categories.FindAsync(vm.Id.Value);
            if (entity is null) return NotFound();

            entity.Name = vm.Name;
            entity.Slug = vm.Slug;
            entity.IsActive = vm.IsActive;
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
