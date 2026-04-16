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
public class CategoriesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var all = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var ordered = BuildCategoryTree(all);
        ViewBag.DepthMap = ordered.ToDictionary(x => x.Category.Id, x => x.Depth);
        ViewBag.ParentNameMap = all.ToDictionary(x => x.Id, x => x.ParentCategoryId.HasValue
            ? all.FirstOrDefault(c => c.Id == x.ParentCategoryId.Value)?.Name ?? "-"
            : "-");

        return View(ordered.Select(x => x.Category).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CategoryEditViewModel();
        await FillParentOptionsAsync(vm, null);
        return View("Edit", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null) return NotFound();

        var vm = new CategoryEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            SkuPrefix = entity.SkuPrefix,
            ParentCategoryId = entity.ParentCategoryId,
            IsActive = entity.IsActive
        };

        await FillParentOptionsAsync(vm, vm.Id);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CategoryEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await FillParentOptionsAsync(vm, vm.Id);
            return View("Edit", vm);
        }

        if (vm.Id.HasValue && vm.ParentCategoryId == vm.Id.Value)
        {
            ModelState.AddModelError(nameof(vm.ParentCategoryId), "Danh mục cha không thể là chính nó.");
            await FillParentOptionsAsync(vm, vm.Id);
            return View("Edit", vm);
        }

        if (vm.Id.HasValue && vm.ParentCategoryId.HasValue)
        {
            var willCycle = await CausesCycleAsync(vm.Id.Value, vm.ParentCategoryId.Value);
            if (willCycle)
            {
                ModelState.AddModelError(nameof(vm.ParentCategoryId), "Không thể chọn danh mục cha vì sẽ tạo vòng lặp cha-con.");
                await FillParentOptionsAsync(vm, vm.Id);
                return View("Edit", vm);
            }
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
        entity.ParentCategoryId = vm.ParentCategoryId;
        entity.IsActive = vm.IsActive;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null) return NotFound();

        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
        {
            TempData["CategoryMessage"] = "Danh mục đã có sản phẩm, không thể xóa. Hãy chuyển sản phẩm sang danh mục khác trước.";
            TempData["CategoryMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var hasChildren = await db.Categories.AnyAsync(c => c.ParentCategoryId == id);
        if (hasChildren)
        {
            TempData["CategoryMessage"] = "Danh mục đang có danh mục con, không thể xóa.";
            TempData["CategoryMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        db.Categories.Remove(entity);
        await db.SaveChangesAsync();
        TempData["CategoryMessage"] = "Đã xóa danh mục thành công.";
        TempData["CategoryMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    private async Task FillParentOptionsAsync(CategoryEditViewModel vm, int? selfId)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var ordered = BuildCategoryTree(categories);
        vm.ParentOptions = [new SelectListItem("-- Không có danh mục cha --", string.Empty, !vm.ParentCategoryId.HasValue)];

        foreach (var row in ordered)
        {
            if (selfId.HasValue && row.Category.Id == selfId.Value) continue;
            vm.ParentOptions.Add(new SelectListItem(
                $"{new string('—', row.Depth)} {(row.Depth > 0 ? "" : "")} {row.Category.Name}".Trim(),
                row.Category.Id.ToString(),
                vm.ParentCategoryId == row.Category.Id
            ));
        }
    }

    private async Task<bool> CausesCycleAsync(int categoryId, int parentId)
    {
        var cursor = parentId;
        while (true)
        {
            if (cursor == categoryId) return true;
            var parent = await db.Categories
                .AsNoTracking()
                .Where(c => c.Id == cursor)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync();
            if (!parent.HasValue) break;
            cursor = parent.Value;
        }

        return false;
    }

    private static List<CategoryTreeRow> BuildCategoryTree(List<Category> categories)
    {
        var roots = categories
            .Where(c => !c.ParentCategoryId.HasValue)
            .OrderBy(c => c.Name)
            .ToList();

        var byParent = categories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).ToList());

        var result = new List<CategoryTreeRow>();

        void Walk(int parentId, int depth)
        {
            if (!byParent.TryGetValue(parentId, out var children)) return;
            foreach (var c in children)
            {
                result.Add(new CategoryTreeRow(c, depth));
                Walk(c.Id, depth + 1);
            }
        }

        foreach (var root in roots)
        {
            result.Add(new CategoryTreeRow(root, 0));
            Walk(root.Id, 1);
        }

        var existingIds = result.Select(x => x.Category.Id).ToHashSet();
        foreach (var orphan in categories.Where(c => !existingIds.Contains(c.Id)).OrderBy(c => c.Name))
        {
            result.Add(new CategoryTreeRow(orphan, 0));
        }

        return result;
    }

    private sealed record CategoryTreeRow(Category Category, int Depth);
}
