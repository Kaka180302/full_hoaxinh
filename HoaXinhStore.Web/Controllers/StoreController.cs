using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Services.Notifications;
using HoaXinhStore.Web.Services.Payments;
using HoaXinhStore.Web.Services.Policies;
using HoaXinhStore.Web.Services;
using HoaXinhStore.Web.Services.Inventory;
using HoaXinhStore.Web.Services.Checkout;
using HoaXinhStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HoaXinhStore.Web.Controllers;

public class StoreController(
    AppDbContext db,
    IVnpayService vnpayService,
    IEmailService emailService,
    IPolicyContentService policyContentService,
    IInventoryService inventoryService,
    IOrderCheckoutService orderCheckoutService) : Controller
{
    private const string CartCookieName = "hx_cart_token";

    [HttpGet]
    public async Task<IActionResult> TrackOrder(string? orderNo = null, string? phoneNumber = null)
    {
        var model = new OrderTrackingViewModel
        {
            OrderNo = orderNo ?? string.Empty,
            PhoneNumber = phoneNumber ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(orderNo) || !string.IsNullOrWhiteSpace(phoneNumber))
        {
            model = await BuildTrackOrderResultAsync(model);
        }
        return View("TrackOrder", new TrackOrderPageViewModel
        {
            HeaderData = await BuildHeaderViewModelAsync(),
            Tracking = model
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrackOrder(OrderTrackingViewModel model)
    {
        var result = await BuildTrackOrderResultAsync(model);
        return View("TrackOrder", new TrackOrderPageViewModel
        {
            HeaderData = await BuildHeaderViewModelAsync(),
            Tracking = result
        });
    }

    [HttpGet]
    public async Task<IActionResult> TrackOrderLookup(string orderNo = "", string phoneNumber = "")
    {
        var result = await BuildTrackOrderResultAsync(new OrderTrackingViewModel
        {
            OrderNo = orderNo,
            PhoneNumber = phoneNumber
        });
        return Json(result);
    }

    private async Task<OrderTrackingViewModel> BuildTrackOrderResultAsync(OrderTrackingViewModel model)
    {
        model.HasSearched = true;

        var orderNo = (model.OrderNo ?? string.Empty).Trim();
        var phone = (model.PhoneNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(orderNo) && string.IsNullOrWhiteSpace(phone))
        {
            model.Found = false;
            model.Message = "Vui lòng nhập mã đơn hoặc số điện thoại.";
            return model;
        }

        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Timelines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            query = query.Where(o => o.OrderNo == orderNo);
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            query = query.Where(o => o.Customer != null && o.Customer.Phone == phone);
        }

        var order = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (order is null)
        {
            model.Found = false;
            model.Message = "Không tìm thấy đơn hàng phù hợp. Vui lòng kiểm tra lại thông tin.";
            return model;
        }

        ParseShippingNote(order.Note, out var carrier, out var trackingCode, out var shippingNote);
        var isPaymentTimeoutCancelled = order.Timelines.Any(t => t.Action == "PaymentTimeoutCancelled");
        if (isPaymentTimeoutCancelled && string.IsNullOrWhiteSpace(shippingNote))
        {
            shippingNote = "Đơn hàng đã tự hủy do quá hạn thanh toán.";
        }
        var shippingAddress = order.Customer?.Addresses?.FirstOrDefault(a => a.IsDefault)?.AddressLine
                              ?? order.Customer?.Addresses?.FirstOrDefault()?.AddressLine
                              ?? "Chưa có địa chỉ";

        model.Found = true;
        model.Order = new OrderTrackingResult
        {
            OrderNo = order.OrderNo,
            CustomerName = order.Customer?.FullName ?? string.Empty,
            PhoneNumber = order.Customer?.Phone ?? string.Empty,
            ShippingAddress = shippingAddress,
            OrderStatus = ToOrderStatusVi(order.OrderStatus),
            PaymentStatus = ToPaymentStatusVi(order.PaymentStatus),
            PaymentMethod = ToPaymentMethodVi(order.PaymentMethod.ToString()),
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAtUtc,
            TrackingCode = trackingCode,
            ShippingCarrier = carrier,
            ShippingNote = shippingNote
        };

        return model;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new StoreCategoryViewModel
            {
                Id = c.Id,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                Slug = string.IsNullOrWhiteSpace(c.Slug) ? "all" : c.Slug
            })
            .ToListAsync();
        ApplyCategoryDepths(categories);

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
                SalePrice = p.SalePrice,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                BrandName = p.Brand != null ? p.Brand.Name : string.Empty,
                BrandImageUrl = p.Brand != null ? p.Brand.ImageUrl : string.Empty,
                IsPreOrderEnabled = p.StockQuantity == 0,
                Summary = p.Summary,
                Description = p.Descriptions,
                CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug,
                CategoryName = p.Category.Name,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new StoreVariantViewModel
                    {
                        Id = v.Id,
                        Sku = v.Sku,
                        Name = v.Name,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.AvailableStock,
                        ReservedStock = v.ReservedStock,
                        AvailableStock = v.AvailableStock,
                        IsDefault = v.IsDefault
                    }).ToList()
            })
            .ToListAsync();
        ApplyProductMeta(products);

        var soldByProduct = await db.OrderItems
            .AsNoTracking()
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Sold);

        var featuredProducts = products
            .OrderByDescending(p => soldByProduct.TryGetValue(p.Id, out var sold) ? sold : 0)
            .ThenByDescending(p => p.Id)
            .Take(8)
            .ToList();

        var categoryById = categories.ToDictionary(c => c.Id);
        int ResolveRootParentId(int categoryId)
        {
            var currentId = categoryId;
            var guard = 0;
            while (guard++ < 32 && categoryById.TryGetValue(currentId, out var current) && current.ParentCategoryId.HasValue)
            {
                currentId = current.ParentCategoryId.Value;
            }
            return currentId;
        }

        var rootParents = categories
            .Where(c => !c.ParentCategoryId.HasValue)
            .OrderBy(c => c.Name)
            .ToList();

        var rootIdByProductId = products.ToDictionary(
            p => p.Id,
            p =>
            {
                var category = categories.FirstOrDefault(c => string.Equals(c.Slug, p.CategorySlug, StringComparison.OrdinalIgnoreCase));
                return category is null ? 0 : ResolveRootParentId(category.Id);
            });

        var categorySections = rootParents
            .Select(parent => new CategorySectionViewModel
            {
                Category = parent,
                Products = products
                    .Where(p => rootIdByProductId.TryGetValue(p.Id, out var rootId) && rootId == parent.Id)
                    .OrderByDescending(p => soldByProduct.TryGetValue(p.Id, out var sold) ? sold : 0)
                    .ThenByDescending(p => p.Id)
                    .Take(5)
                    .ToList()
            })
            .Where(s => s.Products.Count > 0)
            .ToList();

        var model = new StoreIndexViewModel
        {
            Categories = categories,
            Products = products,
            FeaturedProducts = featuredProducts,
            CategorySections = categorySections,
            Brands = await db.CategoryBrands
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new StoreBrandViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    ImageUrl = x.ImageUrl
                })
                .ToListAsync(),
            PolicyData = await policyContentService.GetAllAsync()
        };

        return View(model);
    }

    private async Task<StoreIndexViewModel> BuildHeaderViewModelAsync()
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new StoreCategoryViewModel
            {
                Id = c.Id,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                Slug = string.IsNullOrWhiteSpace(c.Slug) ? "all" : c.Slug
            })
            .ToListAsync();
        ApplyCategoryDepths(categories);
        return new StoreIndexViewModel { Categories = categories };
    }

    [HttpGet]
    public async Task<IActionResult> Products(string q = "", string category = "all", string brand = "all", bool inStockOnly = false, string sort = "newest", decimal? minPrice = null, decimal? maxPrice = null, string filterLabel = "", string priceRanges = "", string statusFilters = "")
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new StoreCategoryViewModel
            {
                Id = c.Id,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                Slug = string.IsNullOrWhiteSpace(c.Slug) ? "all" : c.Slug
            })
            .ToListAsync();
        ApplyCategoryDepths(categories);

        var query = db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            var lowered = keyword.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(lowered) ||
                p.Sku.ToLower().Contains(lowered) ||
                p.Variants.Any(v => v.IsActive && (v.Name.ToLower().Contains(lowered) || v.Sku.ToLower().Contains(lowered))));
        }

        var selectedCategories = (category ?? "all")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.Equals(x, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedCategories.Count > 0)
        {
            var selectedWithChildren = ExpandCategorySlugsWithDescendants(selectedCategories, categories);
            query = query.Where(p => p.Category != null && selectedWithChildren.Contains(p.Category.Slug));
        }

        var selectedBrands = (brand ?? "all")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.Equals(x, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedBrands.Count > 0)
        {
            query = query.Where(p => p.Brand != null && selectedBrands.Contains(p.Brand.Name));
        }
        if (inStockOnly)
        {
            query = query.Where(p => p.StockQuantity > 0 || p.Variants.Any(v => v.IsActive && v.AvailableStock > 0));
        }

        var selectedStatuses = (statusFilters ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (selectedStatuses.Contains("in-stock"))
        {
            query = query.Where(p => p.StockQuantity > 0 || p.Variants.Any(v => v.IsActive && v.AvailableStock > 0));
        }

        var selectedPriceRanges = (priceRanges ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedPriceRanges.Count > 0)
        {
            query = query.Where(p =>
                selectedPriceRanges.Contains("0-100000") && (p.SalePrice ?? p.Price) < 100000
                || selectedPriceRanges.Contains("100000-500000") && (p.SalePrice ?? p.Price) >= 100000 && (p.SalePrice ?? p.Price) <= 500000
                || selectedPriceRanges.Contains("500000-1000000") && (p.SalePrice ?? p.Price) >= 500000 && (p.SalePrice ?? p.Price) <= 1000000
                || selectedPriceRanges.Contains("1000000-plus") && (p.SalePrice ?? p.Price) > 1000000
            );
        }
        else
        {
            if (minPrice.HasValue)
            {
                query = query.Where(p => (p.SalePrice ?? p.Price) >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => (p.SalePrice ?? p.Price) <= maxPrice.Value);
            }
        }

        query = (sort ?? "newest").ToLowerInvariant() switch
        {
            "price-asc" => query.OrderBy(p => p.SalePrice ?? p.Price).ThenByDescending(p => p.Id),
            "price-desc" => query.OrderByDescending(p => p.SalePrice ?? p.Price).ThenByDescending(p => p.Id),
            "name-asc" => query.OrderBy(p => p.Name),
            "name-desc" => query.OrderByDescending(p => p.Name),
            _ => query.OrderByDescending(p => p.Id)
        };

        var products = await query.Select(p => new StoreProductViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            SalePrice = p.SalePrice,
            StockQuantity = p.StockQuantity,
            ImageUrl = p.ImageUrl,
            BrandName = p.Brand != null ? p.Brand.Name : string.Empty,
            BrandImageUrl = p.Brand != null ? p.Brand.ImageUrl : string.Empty,
            IsPreOrderEnabled = p.StockQuantity == 0,
            Summary = p.Summary,
            Description = p.Descriptions,
            CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug,
            CategoryName = p.Category.Name,
            Variants = p.Variants
                .Where(v => v.IsActive)
                .OrderBy(v => v.SortOrder)
                .Select(v => new StoreVariantViewModel
                {
                    Id = v.Id,
                    Sku = v.Sku,
                    Name = v.Name,
                    Price = v.Price,
                    SalePrice = v.SalePrice,
                    StockQuantity = v.AvailableStock,
                    ReservedStock = v.ReservedStock,
                    AvailableStock = v.AvailableStock,
                    IsDefault = v.IsDefault
                }).ToList()
        }).ToListAsync();
        ApplyProductMeta(products);

        if (selectedStatuses.Contains("bestseller") || selectedStatuses.Contains("featured"))
        {
            var soldByProduct = await db.OrderItems
                .AsNoTracking()
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Sold);

            var featuredIds = products
                .OrderByDescending(p => soldByProduct.TryGetValue(p.Id, out var sold) ? sold : 0)
                .ThenByDescending(p => p.Id)
                .Take(12)
                .Select(p => p.Id)
                .ToHashSet();

            products = products
                .Where(p =>
                    (selectedStatuses.Contains("bestseller") && soldByProduct.TryGetValue(p.Id, out var soldQty) && soldQty > 0) ||
                    (selectedStatuses.Contains("featured") && featuredIds.Contains(p.Id)) ||
                    (selectedStatuses.Contains("sale") &&
                        ((p.SalePrice.HasValue && p.SalePrice.Value > 0 && p.SalePrice.Value < p.Price) ||
                         p.Variants.Any(v => v.SalePrice.HasValue && v.SalePrice.Value > 0 && v.SalePrice.Value < v.Price))) ||
                    (selectedStatuses.Contains("in-stock") && p.StockQuantity > 0))
                .ToList();
        }
        else if (selectedStatuses.Contains("sale"))
        {
            products = products
                .Where(p =>
                    (p.SalePrice.HasValue && p.SalePrice.Value > 0 && p.SalePrice.Value < p.Price) ||
                    p.Variants.Any(v => v.SalePrice.HasValue && v.SalePrice.Value > 0 && v.SalePrice.Value < v.Price))
                .ToList();
        }
        var brands = await db.CategoryBrands.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => x.Name).Distinct().ToListAsync();

        return View(new ProductListPageViewModel
        {
            Categories = categories,
            Products = products,
            Brands = brands,
            Query = q,
            Category = category,
            Brand = brand,
            InStockOnly = inStockOnly,
            Sort = sort,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            FilterLabel = filterLabel,
            PriceRanges = priceRanges,
            StatusFilters = statusFilters
        });
    }

    private static void ApplyCategoryDepths(List<StoreCategoryViewModel> categories)
    {
        var childrenByParent = categories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var c in categories) c.Depth = 0;

        void Walk(int parentId, int depth)
        {
            if (!childrenByParent.TryGetValue(parentId, out var children)) return;
            foreach (var child in children)
            {
                child.Depth = depth;
                Walk(child.Id, depth + 1);
            }
        }

        foreach (var root in categories.Where(c => !c.ParentCategoryId.HasValue))
        {
            root.Depth = 0;
            Walk(root.Id, 1);
        }
    }

    private static HashSet<string> ExpandCategorySlugsWithDescendants(IEnumerable<string> selectedSlugs, List<StoreCategoryViewModel> categories)
    {
        var slugToCategory = categories
            .Where(c => !string.IsNullOrWhiteSpace(c.Slug) && !string.Equals(c.Slug, "all", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(c => c.Slug, c => c, StringComparer.OrdinalIgnoreCase);
        var childrenByParent = categories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<StoreCategoryViewModel>();

        foreach (var slug in selectedSlugs.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (slugToCategory.TryGetValue(slug, out var category))
            {
                queue.Enqueue(category);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!result.Add(current.Slug)) continue;
            if (!childrenByParent.TryGetValue(current.Id, out var children)) continue;
            foreach (var child in children) queue.Enqueue(child);
        }

        return result;
    }

    [HttpGet]
    public async Task<IActionResult> ProductDetail(int id)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null && p.Id == id)
            .Select(p => new StoreProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                SalePrice = p.SalePrice,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                BrandName = p.Brand != null ? p.Brand.Name : string.Empty,
                BrandImageUrl = p.Brand != null ? p.Brand.ImageUrl : string.Empty,
                IsPreOrderEnabled = p.StockQuantity == 0,
                Summary = p.Summary,
                Description = p.Descriptions,
                CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug,
                CategoryName = p.Category.Name,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new StoreVariantViewModel
                    {
                        Id = v.Id,
                        Sku = v.Sku,
                        Name = v.Name,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.AvailableStock,
                        ReservedStock = v.ReservedStock,
                        AvailableStock = v.AvailableStock,
                        IsDefault = v.IsDefault
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        if (product is null)
        {
            return NotFound();
        }
        ApplyProductMeta([product]);

        var related = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Id != id && p.Category != null && p.Category.Slug == product.CategorySlug)
            .OrderByDescending(p => p.Id)
            .Take(8)
            .Select(p => new StoreProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                SalePrice = p.SalePrice,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                BrandName = p.Brand != null ? p.Brand.Name : string.Empty,
                BrandImageUrl = p.Brand != null ? p.Brand.ImageUrl : string.Empty,
                IsPreOrderEnabled = p.StockQuantity == 0,
                Summary = p.Summary,
                Description = p.Descriptions,
                CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug,
                CategoryName = p.Category.Name,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new StoreVariantViewModel
                    {
                        Id = v.Id,
                        Sku = v.Sku,
                        Name = v.Name,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.AvailableStock,
                        ReservedStock = v.ReservedStock,
                        AvailableStock = v.AvailableStock,
                        IsDefault = v.IsDefault
                    }).ToList()
            })
            .ToListAsync();
        ApplyProductMeta(related);

        var attributeCatalog = await db.ProductAttributes
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                id = a.Id,
                name = a.Name,
                values = a.Values
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => v.Id)
                    .Select(v => v.Value)
                    .ToList()
            })
            .ToListAsync();
        ViewBag.AttributeCatalog = attributeCatalog;
        var valueToAttribute = attributeCatalog
            .SelectMany(a => a.values.Select(v => new
            {
                Key = (v ?? string.Empty).Trim().ToLowerInvariant(),
                AttributeName = (a.name ?? string.Empty).Trim()
            }))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.AttributeName))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().AttributeName);

        var summaryMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in product.Variants ?? [])
        {
            var parts = (variant.Name ?? string.Empty)
                .Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var rawPart in parts)
            {
                var cleanedKey = System.Text.RegularExpressions.Regex
                    .Replace(rawPart, @"\(\s*x\s*\d+\s*\)", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    .Trim()
                    .ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(cleanedKey)) continue;
                var attrName = valueToAttribute.TryGetValue(cleanedKey, out var mapped)
                    ? mapped
                    : "Thuộc tính";
                if (!summaryMap.TryGetValue(attrName, out var values))
                {
                    values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    summaryMap[attrName] = values;
                }
                values.Add(rawPart.Trim());
            }
        }
        ViewBag.VariantAttributeSummary = summaryMap
            .Select(kv => new { Name = kv.Key, Values = kv.Value.OrderBy(x => x).ToList() })
            .OrderBy(x => x.Name)
            .ToList();

        return View(new ProductDetailPageViewModel
        {
            Product = product,
            RelatedProducts = related
        });
    }

    [HttpGet]
    public async Task<IActionResult> StorefrontVersion()
    {
        var productRows = await db.Products
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.IsActive,
                p.Price,
                p.SalePrice,
                p.StockQuantity,
                Variants = p.Variants
                    .Select(v => new { v.Id, v.IsActive, v.Price, v.SalePrice, v.AvailableStock, v.ReservedStock })
                    .OrderBy(v => v.Id)
                    .ToList()
            })
            .OrderBy(p => p.Id)
            .ToListAsync();

        var brandRows = await db.CategoryBrands
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.IsActive, b.Name, b.ImageUrl, b.CategoryId })
            .ToListAsync();

        var payload = System.Text.Json.JsonSerializer.Serialize(new { productRows, brandRows });
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var version = Convert.ToHexString(hashBytes);
        return Json(new { version, generatedAt = DateTimeOffset.UtcNow });
    }

    [HttpPost]
    public async Task<IActionResult> SyncCart([FromBody] CartSyncRequest? request)
    {
        var rows = request?.Items ?? [];
        var productIds = rows.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.ImageUrl,
                p.Price,
                p.SalePrice,
                p.StockQuantity,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .Select(v => new
                    {
                        v.Id,
                        v.Name,
                        v.Price,
                        v.SalePrice,
                        Stock = v.AvailableStock
                    }).ToList()
            })
            .ToListAsync();

        var productMap = products.ToDictionary(x => x.Id, x => x);
        var responseItems = new List<CartSyncResponseItem>();

        foreach (var row in rows)
        {
            if (!productMap.TryGetValue(row.ProductId, out var product))
            {
                continue;
            }

            var variant = row.VariantId > 0 ? product.Variants.FirstOrDefault(v => v.Id == row.VariantId) : null;
            var unitName = !string.IsNullOrWhiteSpace(row.UnitName)
                ? row.UnitName
                : (variant?.Name ?? string.Empty);
            var unitFactor = Math.Max(1, row.UnitFactor);

            var price = variant?.Price ?? product.Price;
            var salePrice = variant?.SalePrice ?? product.SalePrice;
            var stock = Math.Max(0, variant?.Stock ?? product.StockQuantity);
            var qty = Math.Max(1, row.Qty);

            responseItems.Add(new CartSyncResponseItem
            {
                ProductId = row.ProductId,
                VariantId = variant?.Id ?? 0,
                Name = product.Name,
                Image = product.ImageUrl,
                Price = price,
                SalePrice = salePrice,
                Stock = stock,
                Qty = Math.Min(qty, Math.Max(1, stock == 0 ? 1 : stock)),
                UnitName = unitName,
                UnitFactor = unitFactor,
                VariantName = variant?.Name ?? row.VariantName ?? string.Empty
            });
        }

        return Json(new { ok = true, items = responseItems });
    }

    [HttpGet]
    public async Task<IActionResult> CartItems()
    {
        var token = EnsureCartToken();
        var cart = await GetOrCreateCartAsync(token);
        var items = await BuildCartResponseAsync(cart);
        return Json(new { ok = true, items });
    }

    [HttpPost]
    public async Task<IActionResult> CartReplace([FromBody] CartReplaceRequest? request)
    {
        var token = EnsureCartToken();
        var cart = await GetOrCreateCartAsync(token);
        var rows = request?.Items ?? [];

        // Xóa set-based để tránh lỗi concurrency khi nhiều request đồng thời sync cart.
        await db.CartItems
            .Where(x => x.CartId == cart.Id)
            .ExecuteDeleteAsync();

        var distinctRows = rows
            .GroupBy(x => new { x.ProductId, VariantId = x.VariantId ?? 0 })
            .Select(g => g.First())
            .ToList();

        foreach (var row in distinctRows)
        {
            if (row.ProductId <= 0) continue;
            db.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = row.ProductId,
                VariantId = row.VariantId > 0 ? row.VariantId : null,
                Quantity = Math.Max(1, row.Qty),
                Checked = row.Checked,
                UnitName = (row.UnitName ?? string.Empty).Trim(),
                UnitFactor = Math.Max(1, row.UnitFactor)
            });
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var items = await BuildCartResponseAsync(cart);
        return Json(new { ok = true, items });
    }

    [HttpGet]
    public async Task<IActionResult> Policy(string key = "about")
    {
        var all = await policyContentService.GetAllAsync();
        if (!all.TryGetValue(key, out var current))
        {
            var first = all.FirstOrDefault();
            key = first.Key;
            current = first.Value ?? new PolicyContentItem();
        }

        ViewBag.PolicyKey = key;
        ViewBag.PolicyTitle = current.Title;
        ViewBag.PolicyContent = current.Content;
        ViewBag.PolicySource = current.Source;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SubmitPreOrderPopup([FromBody] PreOrderPopupRequest request)
    {
        var productId = request.ProductId;
        var customerName = (request.CustomerName ?? string.Empty).Trim();
        var phoneNumber = (request.PhoneNumber ?? string.Empty).Trim();
        var address = (request.Address ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();
        var note = (request.Note ?? string.Empty).Trim();
        if (productId <= 0 || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(new { ok = false, message = "Vui lòng nhập đủ thông tin bắt buộc." });
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product is null)
        {
            return NotFound(new { ok = false, message = "Không tìm thấy sản phẩm." });
        }

        var qty = Math.Max(1, request.Quantity);
        db.PreOrderRequests.Add(new PreOrderRequest
        {
            ProductId = product.Id,
            ProductNameSnapshot = product.Name,
            ProductSkuSnapshot = product.Sku,
            RequestedQuantity = qty,
            AvailableQuantity = Math.Max(0, product.StockQuantity),
            MissingQuantity = qty,
            CustomerName = customerName,
            PhoneNumber = phoneNumber,
            Email = email,
            Address = address,
            DepositPercent = 0,
            UnitPriceSnapshot = product.SalePrice ?? product.Price,
            PreOrderAmount = (product.SalePrice ?? product.Price) * qty,
            DepositAmount = 0,
            Status = "Pending"
        });
        await db.SaveChangesAsync();

        await emailService.SendPreOrderRequestAsync(
            product.Name,
            product.Sku,
            qty,
            customerName,
            phoneNumber,
            email,
            address,
            note);
        await emailService.SendPreOrderCustomerConfirmAsync(
            product.Name,
            product.Sku,
            qty,
            customerName,
            phoneNumber,
            email,
            address);
        return Json(new
        {
            ok = true,
            title = "Gửi yêu cầu thành công",
            message = "Hoa Xinh đã nhận thông tin đặt trước. Nhân viên sẽ liên hệ xác nhận đơn sớm nhất. Cảm ơn bạn đã tin tưởng mua sắm."
        });
    }

    [HttpGet]
    public async Task<IActionResult> Cart()
    {
        var suggested = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null)
            .OrderByDescending(p => p.Id)
            .Take(8)
            .Select(p => new StoreProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                SalePrice = p.SalePrice,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                BrandName = p.Brand != null ? p.Brand.Name : string.Empty,
                BrandImageUrl = p.Brand != null ? p.Brand.ImageUrl : string.Empty,
                IsPreOrderEnabled = p.StockQuantity == 0,
                Summary = p.Summary,
                Description = p.Descriptions,
                CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug,
                CategoryName = p.Category.Name,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new StoreVariantViewModel
                    {
                        Id = v.Id,
                        Sku = v.Sku,
                        Name = v.Name,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.AvailableStock,
                        ReservedStock = v.ReservedStock,
                        AvailableStock = v.AvailableStock,
                        IsDefault = v.IsDefault
                    }).ToList()
            })
            .ToListAsync();
        ApplyProductMeta(suggested);

        return View(new CartPageViewModel
        {
            SuggestedProducts = suggested
        });
    }

    [HttpGet]
    public IActionResult Checkout()
    {
        ViewBag.AutoQrOrderNo = TempData["AutoQrOrderNo"]?.ToString() ?? string.Empty;
        ViewBag.AutoQrAmount = TempData["AutoQrAmount"]?.ToString() ?? string.Empty;
        ViewBag.AutoQrImageUrl = TempData["AutoQrImageUrl"]?.ToString() ?? string.Empty;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutFormViewModel request)
    {
        var paymentMethod = orderCheckoutService.ParsePaymentMethod(request.PaymentMethod);
        var normalizedMethod = (request.PaymentMethod ?? "COD").Trim().ToUpperInvariant();

        if (paymentMethod == PaymentMethod.QRPAY && !request.QrPayConfirmed)
        {
            TempData["CheckoutMessage"] = "Vui lòng xác nhận đã thanh toán QR trước khi đặt hàng.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Checkout));
        }
        var checkoutResult = await orderCheckoutService.CreateOrderAsync(new CheckoutRequestData
        {
            CustomerName = request.CustomerName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            PaymentMethodRaw = request.PaymentMethod,
            IsExportInvoice = request.IsExportInvoice,
            VatCompanyName = request.VatCompanyName,
            VatTaxCode = request.VatTaxCode,
            VatCompanyAddress = request.VatCompanyAddress,
            VatEmail = request.VatEmail,
            Items = request.Items.Select(x => new CheckoutItemData
            {
                ProductId = x.ProductId,
                VariantId = x.VariantId,
                Quantity = x.Quantity,
                UnitFactor = x.UnitFactor,
                UnitName = x.UnitName
            }).ToList()
        });
        if (!checkoutResult.Success)
        {
            TempData["CheckoutMessage"] = checkoutResult.ErrorMessage;
            TempData["CheckoutStatus"] = "error";
            return checkoutResult.RedirectToCart ? RedirectToAction(nameof(Cart)) : RedirectToAction(nameof(Index));
        }
        if (checkoutResult.ShouldClearCartImmediately)
        {
            await ClearCartAsync();
        }

        var orderForMail = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == checkoutResult.OrderId);

        if (orderForMail is not null && paymentMethod != PaymentMethod.QRPAY && paymentMethod != PaymentMethod.AUTOQR)
        {
            var trackUrl = Url.Action(
                nameof(TrackOrder),
                "Store",
                new { orderNo = orderForMail.OrderNo, phoneNumber = orderForMail.Customer?.Phone ?? string.Empty },
                Request.Scheme,
                Request.Host.Value);
            await emailService.SendOrderPlacedAsync(orderForMail, trackUrl);
        }

        if (paymentMethod == PaymentMethod.VNPAY)
        {
            var order = await db.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == checkoutResult.OrderId);
            if (order is null)
            {
                TempData["CheckoutMessage"] = "Đơn hàng không tồn tại.";
                TempData["CheckoutStatus"] = "error";
                return RedirectToAction(nameof(Index));
            }
            var clientIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
            var payment = order.Payments.FirstOrDefault();
            if (payment is not null)
            {
                payment.TransactionRef = order.OrderNo;
                await db.SaveChangesAsync();
            }

            var returnUrl = Url.Action(
                nameof(PaymentReturn),
                "Store",
                values: null,
                protocol: Request.Scheme,
                host: Request.Host.Value);

            var payUrl = vnpayService.BuildPaymentUrl(order, clientIp, null, returnUrl);
            return Redirect(payUrl);
        }

        if (paymentMethod == PaymentMethod.AUTOQR)
        {
            var transferContent = $"{checkoutResult.OrderNo} {request.CustomerName}".Trim();
            var qrImageUrl = BuildAutoQrImageUrl(orderForMail?.TotalAmount ?? 0, transferContent);

            TempData["AutoQrOrderNo"] = checkoutResult.OrderNo;
            TempData["AutoQrAmount"] = (orderForMail?.TotalAmount ?? 0).ToString(CultureInfo.InvariantCulture);
            TempData["AutoQrImageUrl"] = qrImageUrl;
            TempData["CheckoutMessage"] = "Vui lòng quét mã QR tự động và xác nhận đã chuyển khoản để hoàn tất thanh toán.";
            TempData["CheckoutStatus"] = "info";
            return RedirectToAction(nameof(Checkout));
        }

        if (paymentMethod == PaymentMethod.QRPAY)
        {
            if (orderForMail is not null)
            {
                var trackUrl = Url.Action(
                    nameof(TrackOrder),
                    "Store",
                    new { orderNo = orderForMail.OrderNo, phoneNumber = orderForMail.Customer?.Phone ?? string.Empty },
                    Request.Scheme,
                    Request.Host.Value);
                await emailService.SendOrderPaymentSuccessAsync(orderForMail, trackUrl);
            }

            TempData["CheckoutMessage"] = "Thanh toán QR thành công. Cảm ơn bạn đã tin tưởng Hoa Xinh Store.";
            TempData["CheckoutStatus"] = "success";
            return RedirectToAction(nameof(Index));
        }

        TempData["CheckoutMessage"] = "Đặt hàng thành công! Hoa Xinh sẽ liên hệ xác nhận đơn sớm nhất.";
        TempData["CheckoutStatus"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReturn()
    {
        var txnRef = Request.Query["vnp_TxnRef"].ToString();
        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        var transactionStatus = Request.Query["vnp_TransactionStatus"].ToString();
        var amountRaw = Request.Query["vnp_Amount"].ToString();

        if (string.IsNullOrWhiteSpace(txnRef))
        {
            TempData["CheckoutMessage"] = "Không tìm thấy mã giao dịch VNPAY.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var order = await db.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderNo == txnRef);

        if (order is null)
        {
            TempData["CheckoutMessage"] = "Đơn hàng không tồn tại.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is null)
        {
            TempData["CheckoutMessage"] = "Không tìm thấy bản ghi thanh toán.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        payment.RawResponseJson = string.Join("&", Request.Query.Select(kv => $"{kv.Key}={kv.Value}"));

        var wasPaid = payment.Status == "Paid";
        var wasTerminalFailure = payment.Status == "Cancelled" || payment.Status == "Failed";
        var validSignature = vnpayService.IsValidSignature(Request.Query);
        var amountValid = long.TryParse(amountRaw, out var amountPaid) && amountPaid == (long)Math.Round(order.TotalAmount * 100m);
        var success = validSignature && amountValid && responseCode == "00" && transactionStatus == "00";

        if (success)
        {
            order.PaymentStatus = "Paid";
            if (!string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                await inventoryService.ConsumeOrderReservationsAsync(order);
            }
            order.OrderStatus = "Confirmed";
            payment.Status = "Paid";
            payment.Provider = "VNPAY";
            payment.PaymentMethod = PaymentMethod.VNPAY.ToString();
            payment.TransactionRef = txnRef;
            payment.PaidAtUtc ??= DateTime.UtcNow;
            TempData["CheckoutMessage"] = "Thanh toán thành công. Cảm ơn bạn đã đặt hàng tại HoaXinh Store.";
            TempData["CheckoutStatus"] = "success";

            if (!wasPaid)
            {
                var trackUrl = Url.Action(
                    nameof(TrackOrder),
                    "Store",
                    new { orderNo = order.OrderNo, phoneNumber = order.Customer?.Phone ?? string.Empty },
                    Request.Scheme,
                    Request.Host.Value);
                await emailService.SendOrderPaymentSuccessAsync(order, trackUrl);
            }
            await ClearCartAsync();
        }
        else
        {
            if (!wasPaid && !wasTerminalFailure)
            {
                await inventoryService.ReleaseOrderReservationsAsync(order);
            }
            order.PaymentStatus = responseCode == "24" ? "Cancelled" : "Failed";
            payment.Status = responseCode == "24" ? "Cancelled" : "Failed";
            payment.TransactionRef = txnRef;
            TempData["CheckoutMessage"] = BuildPaymentFailMessage(responseCode, validSignature, amountValid);
            TempData["CheckoutStatus"] = responseCode == "24" ? "cancelled" : "error";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private static string BuildPaymentFailMessage(string responseCode, bool validSignature, bool amountValid)
    {
        if (!validSignature)
        {
            return "Kết quả thanh toán không hợp lệ (sai chữ ký). Vui lòng liên hệ hỗ trợ.";
        }

        if (!amountValid)
        {
            return "Số tiền thanh toán không khớp đơn hàng. Vui lòng liên hệ hỗ trợ.";
        }

        return responseCode switch
        {
            "24" => "Bạn đã hủy giao dịch thanh toán.",
            "51" => "Tài khoản không đủ số dư để thanh toán.",
            "65" => "Tài khoản vượt quá hạn mức giao dịch trong ngày.",
            "75" => "Ngân hàng đang bảo trì. Vui lòng thử lại sau.",
            _ => $"Thanh toán thất bại (mã lỗi: {responseCode})."
        };
    }

    private static void ApplyProductMeta(IEnumerable<StoreProductViewModel> items)
    {
        foreach (var item in items)
        {
            var parsed = ProductContentMeta.Parse(item.Description);
            item.Description = parsed.CleanDescription;
            item.TechnicalSpecs = parsed.TechnicalSpecs;
            item.UsageGuide = parsed.UsageGuide;
            item.UnitOptions = parsed.UnitOptions;
            item.GalleryImages = parsed.GalleryImages;

            var visibleVariants = (item.Variants ?? [])
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Id)
                .ToList();
            if (visibleVariants.Count > 0)
            {
                var defaultVariant = visibleVariants.First();
                item.Price = defaultVariant.Price;
                item.SalePrice = defaultVariant.SalePrice;
                item.StockQuantity = visibleVariants.Sum(v => Math.Max(0, v.StockQuantity));
                item.IsPreOrderEnabled = item.StockQuantity <= 0;
            }
        }
    }

    private static void ParseShippingNote(string? rawNote, out string carrier, out string trackingCode, out string note)
    {
        carrier = string.Empty;
        trackingCode = string.Empty;
        note = string.Empty;
        if (string.IsNullOrWhiteSpace(rawNote)) return;

        var lines = rawNote.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Đơn vị vận chuyển:", StringComparison.OrdinalIgnoreCase))
            {
                carrier = line.Replace("Đơn vị vận chuyển:", "", StringComparison.OrdinalIgnoreCase).Trim();
            }
            else if (line.StartsWith("Mã vận đơn:", StringComparison.OrdinalIgnoreCase))
            {
                trackingCode = line.Replace("Mã vận đơn:", "", StringComparison.OrdinalIgnoreCase).Trim();
            }
            else if (line.StartsWith("Ghi chú:", StringComparison.OrdinalIgnoreCase))
            {
                note = line.Replace("Ghi chú:", "", StringComparison.OrdinalIgnoreCase).Trim();
            }
        }
    }

    private static string ToOrderStatusVi(string? status) => (status ?? string.Empty) switch
    {
        "PendingConfirm" => "Chờ xác nhận",
        "Confirmed" => "Đã xác nhận",
        "Preparing" => "Đang chuẩn bị hàng",
        "Shipping" => "Đang giao hàng",
        "Completed" => "Hoàn thành",
        "Cancelled" => "Đã hủy",
        "DeliveryFailed" => "Giao thất bại",
        "Returned" => "Hoàn hàng",
        _ => status ?? "-"
    };

    private static string ToPaymentStatusVi(string? status) => (status ?? string.Empty) switch
    {
        "Pending" => "Chưa thanh toán",
        "AwaitingGateway" => "Chờ cổng thanh toán",
        "Paid" => "Đã thanh toán",
        "Failed" => "Thanh toán thất bại",
        "Cancelled" => "Đã hủy thanh toán",
        _ => status ?? "-"
    };

    private static string ToPaymentMethodVi(string? method) => (method ?? string.Empty).ToUpperInvariant() switch
    {
        "COD" => "Thanh toán khi nhận hàng (COD)",
        "VNPAY" => "Thanh toán online (VNPAY)",
        "QRPAY" => "Chuyển khoản QR (QRPAY)",
        "AUTOQR" => "Thanh toán QR tự động",
        _ => method ?? "-"
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmAutoQrPayment([FromForm] string orderNo)
    {
        var order = await db.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

        if (order is null)
        {
            return NotFound(new { ok = false, message = "Không tìm thấy đơn hàng." });
        }

        if (order.PaymentMethod != PaymentMethod.AUTOQR)
        {
            return BadRequest(new { ok = false, message = "Đơn hàng không thuộc phương thức QR tự động." });
        }

        var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is null)
        {
            return BadRequest(new { ok = false, message = "Không tìm thấy giao dịch thanh toán." });
        }

        var wasPaid = string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase);
        if (!wasPaid)
        {
            payment.Provider = "AUTOQR";
            payment.PaymentMethod = PaymentMethod.AUTOQR.ToString();
            payment.TransactionRef = order.OrderNo;
            payment.Status = "Paid";
            payment.PaidAtUtc ??= DateTime.UtcNow;
            order.PaymentStatus = "Paid";

            if (!string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                await inventoryService.ConsumeOrderReservationsAsync(order);
                order.OrderStatus = "Confirmed";
            }

            await db.SaveChangesAsync();
        }

        if (!wasPaid)
        {
            var trackUrl = Url.Action(
                nameof(TrackOrder),
                "Store",
                new { orderNo = order.OrderNo, phoneNumber = order.Customer?.Phone ?? string.Empty },
                Request.Scheme,
                Request.Host.Value);
            await emailService.SendOrderPaymentSuccessAsync(order, trackUrl);
        }
        await ClearCartAsync();

        return Ok(new
        {
            ok = true,
            message = "Thanh toán QR tự động đã được ghi nhận. Cảm ơn bạn đã tin tưởng Hoa Xinh Store."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAutoQrPayment([FromForm] string orderNo)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

        if (order is null)
        {
            return NotFound(new { ok = false, message = "Không tìm thấy đơn hàng." });
        }

        if (order.PaymentMethod != PaymentMethod.AUTOQR)
        {
            return BadRequest(new { ok = false, message = "Đơn hàng không thuộc phương thức QR tự động." });
        }

        var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is null)
        {
            return BadRequest(new { ok = false, message = "Không tìm thấy giao dịch thanh toán." });
        }

        if (string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { ok = false, message = "Đơn hàng đã thanh toán, không thể hủy." });
        }

        if (string.Equals(order.PaymentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(order.PaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            await ClearCartAsync();
            return Ok(new { ok = true, message = "Đơn hàng đã được hủy trước đó." });
        }

        var fromStatus = order.OrderStatus;
        await inventoryService.ReleaseOrderReservationsAsync(order);

        order.OrderStatus = "Cancelled";
        order.PaymentStatus = "Cancelled";
        payment.Status = "Cancelled";
        payment.TransactionRef = order.OrderNo;

        db.OrderTimelines.Add(new OrderTimeline
        {
            OrderId = order.Id,
            Action = "CustomerCancelledAutoQr",
            FromStatus = fromStatus,
            ToStatus = order.OrderStatus,
            Note = "Khách đóng popup QR tự động và hủy thanh toán."
        });

        await db.SaveChangesAsync();
        await ClearCartAsync();

        return Ok(new { ok = true, message = "Bạn đã hủy thanh toán QR tự động. Đơn hàng đã được hủy." });
    }

    private static string BuildAutoQrImageUrl(decimal totalAmount, string transferContent)
    {
        const string bankBin = "970441";
        const string accountNumber = "675704060109421";
        const string accountName = "CT TNHH SX TM&DV HOA XINH";
        var amount = Math.Max(0L, (long)Math.Round(totalAmount, MidpointRounding.AwayFromZero));
        return $"https://img.vietqr.io/image/{bankBin}-{accountNumber}-compact2.png?amount={amount}&addInfo={Uri.EscapeDataString(transferContent)}&accountName={Uri.EscapeDataString(accountName)}";
    }

    private string EnsureCartToken()
    {
        var token = Request.Cookies[CartCookieName];
        if (!string.IsNullOrWhiteSpace(token)) return token;
        token = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(CartCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMonths(6)
        });
        return token;
    }

    private async Task<Cart> GetOrCreateCartAsync(string token)
    {
        var cart = await db.Carts.FirstOrDefaultAsync(x => x.Token == token);
        if (cart is not null) return cart;
        cart = new Cart
        {
            Token = token,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return cart;
    }

    private async Task<List<CartSyncResponseItem>> BuildCartResponseAsync(Cart cart)
    {
        var cartItems = await db.CartItems
            .AsNoTracking()
            .Where(x => x.CartId == cart.Id)
            .ToListAsync();

        if (cartItems.Count == 0) return [];

        var productIds = cartItems.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.ImageUrl,
                p.Price,
                p.SalePrice,
                p.StockQuantity,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .Select(v => new { v.Id, v.Name, v.Price, v.SalePrice, Stock = v.AvailableStock })
                    .ToList()
            })
            .ToListAsync();

        var productMap = products.ToDictionary(x => x.Id, x => x);
        var results = new List<CartSyncResponseItem>();
        foreach (var row in cartItems)
        {
            if (!productMap.TryGetValue(row.ProductId, out var product)) continue;
            var variant = row.VariantId.HasValue ? product.Variants.FirstOrDefault(v => v.Id == row.VariantId.Value) : null;
            var price = variant?.Price ?? product.Price;
            var salePrice = variant?.SalePrice ?? product.SalePrice;
            var stock = Math.Max(0, variant?.Stock ?? product.StockQuantity);
            results.Add(new CartSyncResponseItem
            {
                ProductId = row.ProductId,
                VariantId = variant?.Id ?? 0,
                Name = product.Name,
                Image = product.ImageUrl,
                Price = price,
                SalePrice = salePrice,
                Stock = stock,
                Qty = Math.Min(Math.Max(1, row.Quantity), Math.Max(1, stock == 0 ? 1 : stock)),
                UnitName = !string.IsNullOrWhiteSpace(row.UnitName) ? row.UnitName : (variant?.Name ?? string.Empty),
                UnitFactor = Math.Max(1, row.UnitFactor),
                VariantName = variant?.Name ?? string.Empty,
                Checked = row.Checked
            });
        }
        return results;
    }

    private async Task ClearCartAsync()
    {
        var token = Request.Cookies[CartCookieName];
        if (string.IsNullOrWhiteSpace(token)) return;
        var cart = await db.Carts.FirstOrDefaultAsync(x => x.Token == token);
        if (cart is null) return;
        db.CartItems.RemoveRange(db.CartItems.Where(x => x.CartId == cart.Id));
        cart.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public sealed class CartSyncRequest
    {
        public List<CartSyncRequestItem> Items { get; set; } = [];
    }

    public sealed class CartSyncRequestItem
    {
        public int ProductId { get; set; }
        public int VariantId { get; set; }
        public int Qty { get; set; }
        public int UnitFactor { get; set; } = 1;
        public string? UnitName { get; set; }
        public string? VariantName { get; set; }
    }

    public sealed class CartSyncResponseItem
    {
        public int ProductId { get; set; }
        public int VariantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public int Stock { get; set; }
        public int Qty { get; set; }
        public int UnitFactor { get; set; } = 1;
        public string UnitName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public bool Checked { get; set; } = true;
    }

    public sealed class CartReplaceRequest
    {
        public List<CartReplaceItem> Items { get; set; } = [];
    }

    public sealed class CartReplaceItem
    {
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public int Qty { get; set; } = 1;
        public int UnitFactor { get; set; } = 1;
        public string? UnitName { get; set; }
        public bool Checked { get; set; } = true;
    }
}
