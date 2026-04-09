using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Services.Notifications;
using HoaXinhStore.Web.Services.Payments;
using HoaXinhStore.Web.Services.Policies;
using HoaXinhStore.Web.Services;
using HoaXinhStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Controllers;

public class StoreController(
    AppDbContext db,
    IVnpayService vnpayService,
    IEmailService emailService,
    IPolicyContentService policyContentService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> TrackOrder(string? orderNo = null, string? phoneNumber = null)
    {
        return RedirectToAction(nameof(Index), new { orderNo, phoneNumber });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrackOrder(OrderTrackingViewModel model)
    {
        return RedirectToAction(nameof(Index), new { orderNo = model.OrderNo, phoneNumber = model.PhoneNumber });
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
                        StockQuantity = v.StockQuantity
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

        var categorySections = categories
            .Select(c => new CategorySectionViewModel
            {
                Category = c,
                Products = products
                    .Where(p => string.Equals(p.CategorySlug, c.Slug, StringComparison.OrdinalIgnoreCase))
                    .Take(8)
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
            PolicyData = await policyContentService.GetAllAsync()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Products(string q = "", string category = "all", string brand = "all", bool inStockOnly = false, string sort = "newest", decimal? minPrice = null, decimal? maxPrice = null, string filterLabel = "")
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

        if (!string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Category != null && p.Category.Slug == category);
        }
        if (!string.Equals(brand, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Brand != null && p.Brand.Name == brand);
        }
        if (inStockOnly)
        {
            query = query.Where(p => p.StockQuantity > 0 || p.Variants.Any(v => v.IsActive && v.StockQuantity > 0));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => (p.SalePrice ?? p.Price) >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => (p.SalePrice ?? p.Price) <= maxPrice.Value);
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
                    StockQuantity = v.StockQuantity
                }).ToList()
        }).ToListAsync();
        ApplyProductMeta(products);
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
            FilterLabel = filterLabel
        });
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
                        StockQuantity = v.StockQuantity
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
                        StockQuantity = v.StockQuantity
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

        return View(new ProductDetailPageViewModel
        {
            Product = product,
            RelatedProducts = related
        });
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
        if (request.ProductId <= 0 || string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { ok = false, message = "Vui lòng nhập đủ thông tin bắt buộc." });
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);
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
            CustomerName = request.CustomerName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = (request.Email ?? string.Empty).Trim(),
            Address = (request.Address ?? string.Empty).Trim(),
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
            request.CustomerName,
            request.PhoneNumber,
            request.Email ?? string.Empty,
            request.Address ?? string.Empty,
            request.Note ?? string.Empty);
        return Json(new { ok = true, message = "Đã gửi yêu cầu đặt trước. Nhân viên sẽ gọi xác nhận sớm nhất." });
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
                        StockQuantity = v.StockQuantity
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutFormViewModel request)
    {
        if (request.Items.Count == 0)
        {
            TempData["CheckoutMessage"] = "Vui lòng chọn ít nhất 1 sản phẩm.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != productIds.Count)
        {
            TempData["CheckoutMessage"] = "Có sản phẩm không hợp lệ hoặc đã ngừng bán.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var normalizedMethod = (request.PaymentMethod ?? "COD").Trim().ToUpperInvariant();
        var paymentMethod = normalizedMethod == "COD" ? PaymentMethod.COD : PaymentMethod.VNPAY;
        var vnpBankCode = normalizedMethod switch
        {
            "VNPAY" => string.Empty,
            _ => string.Empty
        };

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out _))
            {
                TempData["CheckoutMessage"] = "Có sản phẩm không hợp lệ.";
                TempData["CheckoutStatus"] = "error";
                return RedirectToAction(nameof(Index));
            }
            if (item.VariantId.HasValue)
            {
                var validVariant = await db.ProductVariants.AnyAsync(v => v.Id == item.VariantId.Value && v.ProductId == item.ProductId && v.IsActive);
                if (!validVariant)
                {
                    TempData["CheckoutMessage"] = "Biến thể sản phẩm không hợp lệ.";
                    TempData["CheckoutStatus"] = "error";
                    return RedirectToAction(nameof(Index));
                }
            }
        }

        using var tx = await db.Database.BeginTransactionAsync();

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Phone == request.PhoneNumber && c.Email == request.Email);

        if (customer is null)
        {
            customer = new Customer
            {
                CustomerType = "Guest",
                FullName = request.CustomerName,
                Phone = request.PhoneNumber,
                Email = request.Email
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }

        var shippingAddress = (request.Address ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(shippingAddress))
        {
            var defaultAddress = await db.CustomerAddresses
                .FirstOrDefaultAsync(a => a.CustomerId == customer.Id && a.IsDefault);

            if (defaultAddress is null)
            {
                db.CustomerAddresses.Add(new CustomerAddress
                {
                    CustomerId = customer.Id,
                    ReceiverName = request.CustomerName,
                    Phone = request.PhoneNumber,
                    AddressLine = shippingAddress,
                    Ward = string.Empty,
                    District = string.Empty,
                    Province = string.Empty,
                    IsDefault = true
                });
            }
            else
            {
                defaultAddress.ReceiverName = request.CustomerName;
                defaultAddress.Phone = request.PhoneNumber;
                defaultAddress.AddressLine = shippingAddress;
            }
        }

        var variantIds = request.Items.Where(i => i.VariantId.HasValue).Select(i => i.VariantId!.Value).Distinct().ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id) && v.IsActive)
            .ToDictionaryAsync(v => v.Id);

        var orderItems = request.Items.Select(i =>
        {
            var qty = Math.Max(1, i.Quantity);
            var unitFactor = Math.Max(1, i.UnitFactor);
            var product = products[i.ProductId];
            var variant = i.VariantId.HasValue && variants.TryGetValue(i.VariantId.Value, out var foundVariant) ? foundVariant : null;
            var stockToDeduct = qty * unitFactor;
            if (variant is not null)
            {
                variant.StockQuantity = Math.Max(0, variant.StockQuantity - stockToDeduct);
            }
            else
            {
                product.StockQuantity = Math.Max(0, product.StockQuantity - stockToDeduct);
            }
            var unitPrice = variant?.SalePrice ?? variant?.Price ?? product.SalePrice ?? product.Price;
            var snapshotSku = variant?.Sku ?? product.Sku;
            var snapshotVariantName = variant?.Name ?? string.Empty;

            return new OrderItem
            {
                ProductId = product.Id,
                VariantId = variant?.Id,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = snapshotSku,
                VariantNameSnapshot = snapshotVariantName,
                Quantity = qty,
                UnitFactor = Math.Max(1, i.UnitFactor),
                UnitName = i.UnitName ?? string.Empty,
                IsPreOrder = (variant?.StockQuantity ?? product.StockQuantity) == 0,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * qty
            };
        }).ToList();

        var subtotal = orderItems.Sum(i => i.LineTotal);
        var order = new Order
        {
            OrderNo = $"HX{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(10, 99)}",
            CustomerId = customer.Id,
            OrderStatus = "PendingConfirm",
            PaymentStatus = paymentMethod == PaymentMethod.COD ? "Pending" : "AwaitingGateway",
            PaymentMethod = paymentMethod,
            Subtotal = subtotal,
            DiscountAmount = 0,
            ShippingFee = 0,
            TotalAmount = subtotal,
            Note = string.Empty,
            IsExportInvoice = request.IsExportInvoice,
            VatCompanyName = request.VatCompanyName ?? string.Empty,
            VatTaxCode = request.VatTaxCode ?? string.Empty,
            VatCompanyAddress = request.VatCompanyAddress ?? string.Empty,
            VatEmail = request.VatEmail ?? string.Empty,
            Items = orderItems,
            Payments = new List<Payment>
            {
                new()
                {
                    Provider = paymentMethod == PaymentMethod.VNPAY ? "VNPAY" : "COD",
                    PaymentMethod = paymentMethod.ToString(),
                    Amount = subtotal,
                    Status = paymentMethod == PaymentMethod.COD ? "Pending" : "Initiated"
                }
            }
        };

        db.Orders.Add(order);
        db.OrderTimelines.Add(new OrderTimeline
        {
            Order = order,
            Action = "Created",
            FromStatus = string.Empty,
            ToStatus = order.OrderStatus,
            Note = "Tạo đơn hàng mới"
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var orderForMail = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        if (orderForMail is not null)
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

            var payUrl = vnpayService.BuildPaymentUrl(order, clientIp, vnpBankCode, returnUrl);
            return Redirect(payUrl);
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
        var validSignature = vnpayService.IsValidSignature(Request.Query);
        var amountValid = long.TryParse(amountRaw, out var amountPaid) && amountPaid == (long)Math.Round(order.TotalAmount * 100m);
        var success = validSignature && amountValid && responseCode == "00" && transactionStatus == "00";

        if (success)
        {
            order.PaymentStatus = "Paid";
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
        }
        else
        {
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
        _ => method ?? "-"
    };
}
