using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Services.Notifications;
using HoaXinhStore.Web.Services.Payments;
using HoaXinhStore.Web.Services.Policies;
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
        var model = new OrderTrackingViewModel
        {
            OrderNo = orderNo ?? string.Empty,
            PhoneNumber = phoneNumber ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            return View(model);
        }

        return View(await BuildTrackOrderResultAsync(model));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrackOrder(OrderTrackingViewModel model)
    {
        return View(await BuildTrackOrderResultAsync(model));
    }

    private async Task<OrderTrackingViewModel> BuildTrackOrderResultAsync(OrderTrackingViewModel model)
    {
        model.HasSearched = true;

        var orderNo = (model.OrderNo ?? string.Empty).Trim();
        var phone = (model.PhoneNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(phone))
        {
            model.Found = false;
            model.Message = "Vui lòng nhập đầy đủ mã đơn và số điện thoại.";
            return model;
        }

        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
                .ThenInclude(c => c.Addresses)
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo && o.Customer != null && o.Customer.Phone == phone);

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
                ImageUrl = p.ImageUrl,
                Summary = p.Summary,
                Description = p.Descriptions,
                CategorySlug = string.IsNullOrWhiteSpace(p.Category!.Slug) ? "all" : p.Category.Slug
            })
            .ToListAsync();

        var model = new StoreIndexViewModel
        {
            Categories = categories,
            Products = products,
            PolicyData = await policyContentService.GetAllAsync()
        };

        return View(model);
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
            var qty = Math.Max(1, item.Quantity);
            if (!products.TryGetValue(item.ProductId, out var product) || product.StockQuantity < qty)
            {
                TempData["CheckoutMessage"] = $"Sản phẩm '{product?.Name ?? "Không xác định"}' không đủ tồn kho.";
                TempData["CheckoutStatus"] = "error";
                return RedirectToAction(nameof(Index));
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

        var orderItems = request.Items.Select(i =>
        {
            var qty = Math.Max(1, i.Quantity);
            var product = products[i.ProductId];
            product.StockQuantity -= qty;

            return new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = product.Sku,
                Quantity = qty,
                UnitPrice = product.Price,
                LineTotal = product.Price * qty
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
