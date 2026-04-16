using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AttributesController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string q = "", string status = "all")
    {
        var query = db.ProductAttributes
            .AsNoTracking()
            .Include(a => a.Values)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(a => a.Name.Contains(q));
        }

        if (status == "active") query = query.Where(a => a.IsActive);
        if (status == "inactive") query = query.Where(a => !a.IsActive);

        var items = await query
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .ToListAsync();

        ViewBag.Query = q;
        ViewBag.Status = status;
        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new ProductAttributeEditViewModel { Values = [new()] });

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.ProductAttributes
            .AsNoTracking()
            .Include(a => a.Values)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (entity is null) return NotFound();

        return View(new ProductAttributeEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            Values = entity.Values
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Id)
                .Select(v => new ProductAttributeValueEditItem
                {
                    Id = v.Id,
                    Value = v.Value,
                    ConversionFactor = Math.Max(0, (int)Math.Round(v.ConversionFactor ?? 1m)),
                    SortOrder = v.SortOrder,
                    IsActive = v.IsActive
                }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProductAttributeEditViewModel vm)
    {
        if (!ModelState.IsValid) return View("Edit", vm);

        ProductAttribute entity;
        if (vm.Id.HasValue)
        {
            entity = await db.ProductAttributes
                .Include(a => a.Values)
                .FirstOrDefaultAsync(a => a.Id == vm.Id.Value) ?? throw new InvalidOperationException("Attribute not found");
        }
        else
        {
            entity = new ProductAttribute();
            db.ProductAttributes.Add(entity);
        }

        entity.Name = vm.Name.Trim();
        entity.Description = (vm.Description ?? string.Empty).Trim();
        entity.SortOrder = vm.SortOrder;
        entity.IsActive = vm.IsActive;
        await db.SaveChangesAsync();

        var existing = await db.ProductAttributeValues.Where(v => v.ProductAttributeId == entity.Id).ToListAsync();
        var rows = (vm.Values ?? []).Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToList();
        var keep = new HashSet<int>();
        foreach (var row in rows)
        {
            if (row.Id.HasValue)
            {
                var ev = existing.FirstOrDefault(x => x.Id == row.Id.Value);
                if (ev is null) continue;
                ev.Value = row.Value.Trim();
                ev.ConversionFactor = Math.Max(0, row.ConversionFactor ?? 1);
                ev.SortOrder = row.SortOrder;
                ev.IsActive = row.IsActive;
                keep.Add(ev.Id);
            }
            else
            {
                db.ProductAttributeValues.Add(new ProductAttributeValue
                {
                    ProductAttributeId = entity.Id,
                    Value = row.Value.Trim(),
                    ConversionFactor = Math.Max(0, row.ConversionFactor ?? 1),
                    SortOrder = row.SortOrder,
                    IsActive = row.IsActive
                });
            }
        }

        var toDelete = existing.Where(x => !keep.Contains(x.Id)).ToList();
        if (toDelete.Count > 0) db.ProductAttributeValues.RemoveRange(toDelete);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var entity = await db.ProductAttributes.FindAsync(id);
        if (entity is null) return NotFound();
        entity.IsActive = !entity.IsActive;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
