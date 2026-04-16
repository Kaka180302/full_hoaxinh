using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Hubs;
using HoaXinhStore.Web.Services;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ProductsController(AppDbContext db, IWebHostEnvironment env, IHubContext<StorefrontHub> hub) : Controller
{
    private const string DefaultSummaryText = "Hàng nhập khẩu chính hãng từ Hàn Quốc\nCam kết bảo hành chính hãng 12 tháng\nĐổi trả dễ dàng trong 7 ngày miễn phí";

    public async Task<IActionResult> Index(string q = "", int? categoryId = null, int page = 1, int pageSize = 10)
    {
        await NormalizeSkusAsync();
        var query = db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .AsQueryable();

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

        var baseStockByProductId = new Dictionary<int, int>();
        foreach (var p in items)
        {
            var active = (p.Variants ?? []).Where(v => v.IsActive).ToList();
            if (active.Count == 0)
            {
                baseStockByProductId[p.Id] = Math.Max(0, p.StockQuantity);
                continue;
            }

            baseStockByProductId[p.Id] = Math.Max(0, active.Sum(v => v.AvailableStock));
        }

        ViewBag.Query = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.BaseStockByProductId = baseStockByProductId;
        ViewBag.Categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? categoryId = null)
    {
        if (!categoryId.HasValue)
        {
            ViewBag.Categories = await db.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewBag.Brands = await db.CategoryBrands
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();
            return View("SelectCategory");
        }

        var vm = new AdminProductEditViewModel
        {
            CategoryId = categoryId.Value,
            BrandId = Request.Query.ContainsKey("brandId") ? int.TryParse(Request.Query["brandId"], out var bid) ? bid : null : null,
            Sku = await GenerateNextSkuAsync(categoryId.Value),
            Summary = DefaultSummaryText,
            Descriptions = "Chưa có mô tả",
            TechnicalSpecs = "Chưa có mô tả",
            UsageGuide = "Chưa có mô tả"
        };
        vm.CaseFactor = 0;
        vm.PackFactor = 0;
        await LoadCategoryOptions(vm);
        await LoadBrandOptions(vm);
        await LoadUnitPresetOptions();
        await LoadAttributeOptions();
        return View("Edit", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BeginCreate(int categoryId, int? brandId = null)
    {
        return RedirectToAction(nameof(Create), new { categoryId, brandId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.Products
            .Include(x => x.Variants)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        var vm = new AdminProductEditViewModel
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Price = entity.Price,
            SalePrice = entity.SalePrice,
            StockQuantity = entity.StockQuantity,
            ImageUrl = entity.ImageUrl,
            Summary = entity.Summary,
            Descriptions = ProductContentMeta.Parse(entity.Descriptions).CleanDescription,
            CategoryId = entity.CategoryId,
            BrandId = entity.BrandId,
            IsActive = entity.IsActive
        };
        var parsed = ProductContentMeta.Parse(entity.Descriptions);
        vm.Summary = string.IsNullOrWhiteSpace(entity.Summary) || string.Equals(entity.Summary.Trim(), "Chưa có mô tả", StringComparison.OrdinalIgnoreCase)
            ? DefaultSummaryText
            : entity.Summary;
        vm.Descriptions = string.IsNullOrWhiteSpace(parsed.CleanDescription) ? "Chưa có mô tả" : parsed.CleanDescription;
        vm.TechnicalSpecs = string.IsNullOrWhiteSpace(parsed.TechnicalSpecs) ? "Chưa có mô tả" : parsed.TechnicalSpecs;
        vm.UsageGuide = string.IsNullOrWhiteSpace(parsed.UsageGuide) ? "Chưa có mô tả" : parsed.UsageGuide;
        vm.UnitOptions = parsed.UnitOptions.Select(u => new AdminProductUnitOptionInput
        {
            Name = u.Name,
            Factor = u.Factor
        }).ToList();
        vm.GalleryImageUrls = (parsed.GalleryImages ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        vm.CaseFactor = parsed.UnitOptions.FirstOrDefault(x => string.Equals(x.Name, "thùng", StringComparison.OrdinalIgnoreCase))?.Factor ?? 0;
        vm.PackFactor = parsed.UnitOptions.FirstOrDefault(x => string.Equals(x.Name, "lốc", StringComparison.OrdinalIgnoreCase))?.Factor ?? 0;

        vm.StockCase = vm.CaseFactor > 0 ? entity.StockQuantity / vm.CaseFactor : 0;
        vm.StockPack = vm.PackFactor > 0 ? entity.StockQuantity / vm.PackFactor : 0;
        vm.Variants = entity.Variants
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .Select(v => new AdminProductVariantInput
            {
                Id = v.Id,
                Sku = v.Sku,
                Name = v.Name,
                Price = v.Price,
                SalePrice = v.SalePrice,
                WeightGram = v.WeightGram,
                LengthMm = v.LengthMm,
                WidthMm = v.WidthMm,
                HeightMm = v.HeightMm,
                ImageUrl = v.ImageUrl,
                StockQuantity = v.StockQuantity,
                IsDefault = v.IsDefault,
                IsActive = v.IsActive,
                SortOrder = v.SortOrder
            })
            .ToList();

        await LoadCategoryOptions(vm);
        await LoadBrandOptions(vm);
        await LoadUnitPresetOptions();
        await LoadAttributeOptions();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var entity = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity is null) return NotFound();
        var parsed = ProductContentMeta.Parse(entity.Descriptions);
        ViewBag.CleanDescription = string.IsNullOrWhiteSpace(parsed.CleanDescription) ? "Chưa có mô tả" : parsed.CleanDescription;
        ViewBag.TechnicalSpecs = string.IsNullOrWhiteSpace(parsed.TechnicalSpecs) ? "Chưa có mô tả" : parsed.TechnicalSpecs;
        ViewBag.UsageGuide = string.IsNullOrWhiteSpace(parsed.UsageGuide) ? "Chưa có mô tả" : parsed.UsageGuide;
        ViewBag.TotalVariantStock = (entity.Variants ?? []).Where(v => v.IsActive).Sum(v => Math.Max(0, v.AvailableStock));
        return View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminProductEditViewModel vm)
    {
        var csvDeleted = (vm.DeletedVariantIdsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(x => x > 0);
        var deletedSet = (vm.DeletedVariantIds ?? [])
            .Concat(csvDeleted)
            .Where(x => x > 0)
            .ToHashSet();
        vm.Variants = (vm.Variants ?? [])
            .Where(v => !(v.Id.HasValue && deletedSet.Contains(v.Id.Value)))
            .ToList();

        if (!ModelState.IsValid)
        {
            await LoadCategoryOptions(vm);
            await LoadBrandOptions(vm);
            await LoadUnitPresetOptions();
            await LoadAttributeOptions();
            return View("Edit", vm);
        }

        if ((vm.Variants ?? []).Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng tạo ít nhất 1 biến thể.");
            await LoadCategoryOptions(vm);
            await LoadBrandOptions(vm);
            await LoadUnitPresetOptions();
            await LoadAttributeOptions();
            return View("Edit", vm);
        }

        if ((vm.Variants ?? []).Any(v => string.IsNullOrWhiteSpace(v.Name)
            || string.IsNullOrWhiteSpace(v.Sku)
            || v.Price <= 0
            || (v.SalePrice.HasValue && v.SalePrice.Value < 0)
            || v.StockQuantity < 0))
        {
            ModelState.AddModelError(string.Empty, "Trong biến thể: Tên, SKU, Giá, Tồn kho là bắt buộc. Giá KM có thể để trống.");
            await LoadCategoryOptions(vm);
            await LoadBrandOptions(vm);
            await LoadUnitPresetOptions();
            await LoadAttributeOptions();
            return View("Edit", vm);
        }

        if ((vm.Variants ?? []).Count > 0)
        {
            var active = vm.Variants.Where(v => v.IsActive).ToList();
            var source = active.Count > 0 ? active : vm.Variants;
            vm.Price = source.Min(v => v.Price);
            vm.SalePrice = source.Min(v => v.SalePrice ?? v.Price);
        }
        else if (vm.SalePrice.HasValue && vm.SalePrice.Value > vm.Price)
        {
            ModelState.AddModelError(nameof(vm.SalePrice), "Giá khuyến mãi không được lớn hơn giá bán.");
            await LoadCategoryOptions(vm);
            await LoadBrandOptions(vm);
            await LoadUnitPresetOptions();
            await LoadAttributeOptions();
            return View("Edit", vm);
        }

        vm.UnitOptions = [];
        vm.Variants = (vm.Variants ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .ToList();
        var summaryText = string.IsNullOrWhiteSpace(vm.Summary) ? DefaultSummaryText : vm.Summary!.Trim();
        var descText = string.IsNullOrWhiteSpace(vm.Descriptions) ? "Chưa có mô tả" : vm.Descriptions!.Trim();
        var techText = string.IsNullOrWhiteSpace(vm.TechnicalSpecs) ? "Chưa có mô tả" : vm.TechnicalSpecs!.Trim();
        var usageText = string.IsNullOrWhiteSpace(vm.UsageGuide) ? "Chưa có mô tả" : vm.UsageGuide!.Trim();
        var galleryImages = new List<string>();
        if (vm.GalleryFiles is { Count: > 0 })
        {
            foreach (var file in vm.GalleryFiles.Where(f => f is { Length: > 0 }))
            {
                galleryImages.Add(await SaveImageAsync(file));
            }
        }
        galleryImages = galleryImages
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Tồn kho sản phẩm được đồng bộ hoàn toàn từ tổng tồn kho biến thể đang hiển thị.
        vm.StockQuantity = 0;
        vm.UnitOptions = SyncUnitOptions(vm.UnitOptions, vm.CaseFactor, vm.PackFactor);

        if (vm.Id is null)
        {
            var imageUrl = vm.ImageFile is { Length: > 0 }
                ? await SaveImageAsync(vm.ImageFile)
                : "/assets/img/logo/hoa_xinh_group_fav.png";
            var entity = new Product
            {
                Sku = string.IsNullOrWhiteSpace(vm.Sku) ? await GenerateNextSkuAsync(vm.CategoryId) : vm.Sku,
                Name = vm.Name,
                Price = vm.Price,
                SalePrice = vm.SalePrice,
                StockQuantity = 0,
                ImageUrl = imageUrl,
                BrandId = vm.BrandId,
                IsPreOrderEnabled = true,
                Summary = summaryText,
                Descriptions = ProductContentMeta.Compose(
                    descText,
                    techText,
                    usageText,
                    vm.UnitOptions.Select(x => new ProductUnitOption { Name = x.Name, Factor = x.Factor }),
                    galleryImages),
                CategoryId = vm.CategoryId,
                IsActive = vm.IsActive
            };
            db.Products.Add(entity);
            await db.SaveChangesAsync();
            await UpsertVariantsAsync(entity.Id, vm);
        }
        else
        {
            var entity = await db.Products
                .Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.Id == vm.Id.Value);
            if (entity is null) return NotFound();

            // Keep existing gallery images, then append newly uploaded files.
            var existingMeta = ProductContentMeta.Parse(entity.Descriptions);
            if (existingMeta.GalleryImages is { Count: > 0 })
            {
                galleryImages = existingMeta.GalleryImages
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
            }

            if (vm.GalleryFiles is { Count: > 0 })
            {
                foreach (var file in vm.GalleryFiles.Where(f => f is { Length: > 0 }))
                {
                    galleryImages.Add(await SaveImageAsync(file));
                }
            }
            galleryImages = galleryImages
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            entity.Sku = vm.Sku;
            entity.Name = vm.Name;
            entity.Price = vm.Price;
            entity.SalePrice = vm.SalePrice;
            entity.StockQuantity = 0;
            if (vm.ImageFile is { Length: > 0 })
            {
                entity.ImageUrl = await SaveImageAsync(vm.ImageFile);
            }
            entity.BrandId = vm.BrandId;
            entity.IsPreOrderEnabled = true;
            entity.Summary = summaryText;
            entity.Descriptions = ProductContentMeta.Compose(
                descText,
                techText,
                usageText,
                vm.UnitOptions.Select(x => new ProductUnitOption { Name = x.Name, Factor = x.Factor }),
                galleryImages);
            entity.CategoryId = vm.CategoryId;
            entity.IsActive = vm.IsActive;
            await UpsertVariantsAsync(entity.Id, vm, entity.Variants.ToList());
        }

        await db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("storefront-updated", new { type = "product", at = DateTimeOffset.UtcNow });
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
        await hub.Clients.All.SendAsync("storefront-updated", new { type = "product", at = DateTimeOffset.UtcNow });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await db.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();

        var hasOrderItems = await db.OrderItems.AnyAsync(x => x.ProductId == id);
        if (hasOrderItems)
        {
            TempData["ProductAdminMessage"] = "Sản phẩm đã phát sinh đơn hàng nên không thể xóa. Bạn có thể dùng Ẩn/Hiện để ngừng bán.";
            TempData["ProductAdminStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var preOrders = await db.PreOrderRequests.Where(x => x.ProductId == id).ToListAsync();
        if (preOrders.Count > 0)
        {
            db.PreOrderRequests.RemoveRange(preOrders);
        }

        db.Products.Remove(entity);
        await db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("storefront-updated", new { type = "product", id, at = DateTimeOffset.UtcNow });
        TempData["ProductAdminMessage"] = "Đã xóa sản phẩm thành công.";
        TempData["ProductAdminStatus"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetVariantTemplate(int sourceProductId)
    {
        var rows = await db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == sourceProductId && v.IsActive)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .Select(v => new
            {
                name = v.Name,
                price = v.Price,
                salePrice = v.SalePrice,
                weightGram = v.WeightGram,
                lengthMm = v.LengthMm,
                widthMm = v.WidthMm,
                heightMm = v.HeightMm,
                imageUrl = v.ImageUrl,
                stockQuantity = v.StockQuantity,
                isDefault = v.IsDefault,
                isActive = v.IsActive,
                sortOrder = v.SortOrder
            })
            .ToListAsync();
        return Json(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUnitPreset(
        string presetName,
        string unitTemplate,
        string unit2Name,
        int unit2Factor,
        string? unit3Name,
        int? unit3Factor)
    {
        var name = (presetName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || unit2Factor <= 0)
        {
            return BadRequest(new { message = "Preset không hợp lệ." });
        }

        var preset = await db.VariantUnitPresets.FirstOrDefaultAsync(x => x.Name == name);
        if (preset is null)
        {
            preset = new VariantUnitPreset { Name = name };
            db.VariantUnitPresets.Add(preset);
        }

        preset.UnitTemplate = string.IsNullOrWhiteSpace(unitTemplate) ? "single" : unitTemplate.Trim();
        preset.Unit2Name = string.IsNullOrWhiteSpace(unit2Name) ? "Hộp" : unit2Name.Trim();
        preset.Unit2Factor = unit2Factor;
        preset.Unit3Name = string.IsNullOrWhiteSpace(unit3Name) ? "Thùng" : unit3Name.Trim();
        preset.Unit3Factor = Math.Max(1, unit3Factor ?? 20);
        preset.IsActive = true;

        await db.SaveChangesAsync();
        return Json(new
        {
            id = preset.Id,
            name = preset.Name,
            unitTemplate = preset.UnitTemplate,
            unit2Name = preset.Unit2Name,
            unit2Factor = preset.Unit2Factor,
            unit3Name = preset.Unit3Name,
            unit3Factor = preset.Unit3Factor
        });
    }

    private async Task LoadCategoryOptions(AdminProductEditViewModel vm)
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

    private async Task LoadBrandOptions(AdminProductEditViewModel vm)
    {
        vm.BrandOptions = await db.CategoryBrands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            })
            .ToListAsync();
    }

    private async Task LoadUnitPresetOptions()
    {
        ViewBag.UnitPresets = await db.VariantUnitPresets
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private async Task LoadAttributeOptions()
    {
        ViewBag.Attributes = await db.ProductAttributes
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Name,
                Values = a.Values
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => v.Id)
                    .Select(v => new
                    {
                        v.Id,
                        v.Value,
                        ConversionFactor = Math.Max(0, (int)Math.Round(v.ConversionFactor ?? 1m))
                    })
                    .ToList()
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

    private static List<string> ParseGalleryUrls(string? raw)
    {
        return (raw ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private async Task<string> GenerateNextSkuAsync(int categoryId)
    {
        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId);
        if (category is null) return "SP-1";
        var root = ResolveSkuPrefix(category);

        var skus = await db.Products
            .AsNoTracking()
            .Where(p => p.Sku.StartsWith(root + "-"))
            .Select(p => p.Sku)
            .ToListAsync();

        var maxNo = 0;
        foreach (var sku in skus)
        {
            var skuParts = sku.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (skuParts.Length < 2) continue;
            if (int.TryParse(skuParts[^1], out var no) && no > maxNo)
            {
                maxNo = no;
            }
        }

        return $"{root}-{(maxNo + 1)}";
    }

    private static List<AdminProductUnitOptionInput> SyncUnitOptions(List<AdminProductUnitOptionInput> source, int caseFactor, int packFactor)
    {
        var map = (source ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Factor > 0)
            .ToDictionary(x => x.Name.Trim().ToLowerInvariant(), x => x.Factor);
        if (caseFactor > 0) map["thùng"] = caseFactor;
        else map.Remove("thùng");
        if (packFactor > 0) map["lốc"] = packFactor;
        else map.Remove("lốc");
        return map.Select(x => new AdminProductUnitOptionInput { Name = x.Key == "thùng" ? "thùng" : (x.Key == "lốc" ? "lốc" : x.Key), Factor = x.Value }).ToList();
    }

    private static string ResolveSkuPrefix(Category category)
    {
        var slug = (category.Slug ?? string.Empty).ToLowerInvariant();
        var name = (category.Name ?? string.Empty).ToLowerInvariant();
        if (slug.Contains("my-pham") || slug.Contains("mypham") || name.Contains("mỹ phẩm") || name.Contains("my pham")) return "MP";
        if (slug.Contains("thiet-bi") || slug.Contains("giadung") || slug.Contains("gia-dung") || name.Contains("thiết bị") || name.Contains("gia dụng")) return "TB";
        if (slug.Contains("thuc-pham") || slug.Contains("thucpham") || name.Contains("thực phẩm") || name.Contains("thuc pham")) return "TP";
        return string.IsNullOrWhiteSpace(category.SkuPrefix) ? "SP" : category.SkuPrefix.Trim().ToUpperInvariant();
    }

    private async Task NormalizeSkusAsync()
    {
        var categories = await db.Categories.AsNoTracking().ToListAsync();
        var products = await db.Products.OrderBy(p => p.CategoryId).ThenBy(p => p.Id).ToListAsync();
        var changed = false;
        var groups = products
            .GroupBy(p =>
            {
                var cat = categories.FirstOrDefault(c => c.Id == p.CategoryId);
                return cat is null ? "SP" : ResolveSkuPrefix(cat);
            })
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var prefix = group.Key;
            var i = 1;
            foreach (var p in group.OrderBy(x => x.Id))
            {
                var expected = $"{prefix}-{i}";
                if (!string.Equals(p.Sku, expected, StringComparison.OrdinalIgnoreCase))
                {
                    p.Sku = expected;
                    changed = true;
                }
                i++;
            }
        }
        if (changed) await db.SaveChangesAsync();
    }

    private async Task UpsertVariantsAsync(int productId, AdminProductEditViewModel vm, List<ProductVariant>? existing = null)
    {
        existing ??= await db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync();
        var csvDeleted = (vm.DeletedVariantIdsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(x => x > 0);
        var deletedSet = (vm.DeletedVariantIds ?? [])
            .Concat(csvDeleted)
            .Where(x => x > 0)
            .ToHashSet();
        if (deletedSet.Count > 0)
        {
            var deleteRows = existing.Where(x => deletedSet.Contains(x.Id)).ToList();
            if (deleteRows.Count > 0)
            {
                var deleteIds = deleteRows.Select(x => x.Id).ToList();
                var referencedVariantIds = await db.OrderItems
                    .AsNoTracking()
                    .Where(x => x.VariantId.HasValue && deleteIds.Contains(x.VariantId.Value))
                    .Select(x => x.VariantId!.Value)
                    .Distinct()
                    .ToListAsync();
                var refSet = referencedVariantIds.ToHashSet();

                var hardDeleteRows = deleteRows.Where(x => !refSet.Contains(x.Id)).ToList();
                if (hardDeleteRows.Count > 0)
                {
                    db.ProductVariants.RemoveRange(hardDeleteRows);
                }

                foreach (var row in deleteRows.Where(x => refSet.Contains(x.Id)))
                {
                    row.IsActive = false;
                    row.IsDefault = false;
                }
                existing = existing.Where(x => !deletedSet.Contains(x.Id)).ToList();
            }
        }
        var rows = vm.Variants;
        if (rows.Count == 0)
        {
            var p = await db.Products.FindAsync(productId);
            if (p is not null)
            {
                rows.Add(new AdminProductVariantInput
                {
                    Name = "Mặc định",
                    Sku = p.Sku + "-V1",
                    Price = p.Price,
                    SalePrice = p.SalePrice,
                    WeightGram = null,
                    LengthMm = null,
                    WidthMm = null,
                    HeightMm = null,
                    ImageUrl = string.Empty,
                    StockQuantity = p.StockQuantity,
                    IsDefault = true,
                    IsActive = true,
                    SortOrder = 0
                });
            }
        }

        // Ensure there is exactly one default visible variant.
        var visibleRows = rows.Where(v => v.IsActive).ToList();
        if (visibleRows.Count > 0)
        {
            var hasDefaultVisible = visibleRows.Any(v => v.IsDefault);
            if (!hasDefaultVisible)
            {
                var firstVisible = visibleRows
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => string.IsNullOrWhiteSpace(v.Sku))
                    .First();
                firstVisible.IsDefault = true;
            }

            foreach (var row in rows.Where(v => !v.IsActive))
            {
                row.IsDefault = false;
            }

            var chosen = visibleRows.First(v => v.IsDefault);
            foreach (var row in visibleRows.Where(v => !ReferenceEquals(v, chosen)))
            {
                row.IsDefault = false;
            }
        }
        else
        {
            foreach (var row in rows)
            {
                row.IsDefault = false;
            }
        }

        for (var idx = 0; idx < rows.Count; idx++)
        {
            var row = rows[idx];
            var imageFile = vm.VariantImageFiles.Count > idx ? vm.VariantImageFiles[idx] : null;
            var variantImageUrl = row.ImageUrl;
            if (imageFile is { Length: > 0 })
            {
                variantImageUrl = await SaveImageAsync(imageFile);
            }

            if (row.Id.HasValue)
            {
                var ev = existing.FirstOrDefault(x => x.Id == row.Id.Value);
                if (ev is null) continue;
                ev.Sku = row.Sku.Trim();
                ev.Name = row.Name.Trim();
                ev.Price = row.Price;
                ev.SalePrice = row.SalePrice;
                ev.WeightGram = row.WeightGram;
                ev.LengthMm = row.LengthMm;
                ev.WidthMm = row.WidthMm;
                ev.HeightMm = row.HeightMm;
                ev.ImageUrl = (variantImageUrl ?? string.Empty).Trim();
                ev.StockQuantity = Math.Max(0, row.StockQuantity);
                ev.ReservedStock = Math.Clamp(ev.ReservedStock, 0, ev.StockQuantity);
                ev.AvailableStock = Math.Max(0, ev.StockQuantity - ev.ReservedStock);
                ev.IsDefault = row.IsDefault;
                ev.IsActive = row.IsActive;
                ev.SortOrder = row.SortOrder;
            }
            else
            {
                db.ProductVariants.Add(new ProductVariant
                {
                    ProductId = productId,
                    Sku = row.Sku.Trim(),
                    Name = row.Name.Trim(),
                    Price = row.Price,
                    SalePrice = row.SalePrice,
                    WeightGram = row.WeightGram,
                    LengthMm = row.LengthMm,
                    WidthMm = row.WidthMm,
                    HeightMm = row.HeightMm,
                    ImageUrl = (variantImageUrl ?? string.Empty).Trim(),
                    StockQuantity = Math.Max(0, row.StockQuantity),
                    ReservedStock = 0,
                    AvailableStock = Math.Max(0, row.StockQuantity),
                    IsDefault = row.IsDefault,
                    IsActive = row.IsActive,
                    SortOrder = row.SortOrder
                });
            }
        }
        await db.SaveChangesAsync();
        var product = await db.Products.FindAsync(productId);
        if (product is not null)
        {
            var activeVariants = await db.ProductVariants
                .Where(v => v.ProductId == productId && v.IsActive)
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.SortOrder)
                .ThenBy(v => v.Id)
                .ToListAsync();
            product.StockQuantity = Math.Max(0, activeVariants.Sum(v => v.AvailableStock));
            product.IsPreOrderEnabled = product.StockQuantity <= 0;
            var defaultVariant = activeVariants.FirstOrDefault();
            if (defaultVariant is not null)
            {
                product.Price = defaultVariant.Price;
                product.SalePrice = defaultVariant.SalePrice;
            }
            await db.SaveChangesAsync();
        }
    }

}





