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
            TempData["CheckoutMessage"] = "Vui long chon it nhat 1 san pham.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != productIds.Count)
        {
            TempData["CheckoutMessage"] = "Co san pham khong hop le hoac da ngung ban.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var normalizedMethod = (request.PaymentMethod ?? "COD").Trim().ToUpperInvariant();
        var paymentMethod = normalizedMethod == "COD" ? PaymentMethod.COD : PaymentMethod.VNPAY;
        var vnpBankCode = normalizedMethod switch
        {
            // Some sandbox terminals do not have direct VNPAYQR channel enabled.
            // Keep empty so user can still choose supported QR/bank options on VNPay page.
            "VNPAY_QR" => string.Empty,
            "VNPAY_BANK" => "VNBANK",
            "VNPAY_ATM" => "VNBANK",
            "VNPAY_INTL" => "INTCARD",
            _ => string.Empty
        };

        foreach (var item in request.Items)
        {
            var qty = Math.Max(1, item.Quantity);
            if (!products.TryGetValue(item.ProductId, out var product) || product.StockQuantity < qty)
            {
                TempData["CheckoutMessage"] = $"San pham '{product?.Name ?? "Khong xac dinh"}' khong du ton kho.";
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

        if (paymentMethod == PaymentMethod.VNPAY)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
            var payment = order.Payments.FirstOrDefault();
            if (payment is not null)
            {
                payment.TransactionRef = order.OrderNo;
                await db.SaveChangesAsync();
            }

            var returnUrl = Url.Action(nameof(PaymentReturn), "Store", null, Request.Scheme);
            var payUrl = vnpayService.BuildPaymentUrl(order, clientIp, vnpBankCode, returnUrl);
            return Redirect(payUrl);
        }

        TempData["CheckoutMessage"] = "Dat hang thanh cong! Hoa Xinh se lien he xac nhan don som nhat.";
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
            TempData["CheckoutMessage"] = "Khong tim thay ma giao dich VNPAY.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderNo == txnRef);

        if (order is null)
        {
            TempData["CheckoutMessage"] = "Don hang khong ton tai.";
            TempData["CheckoutStatus"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is null)
        {
            TempData["CheckoutMessage"] = "Khong tim thay ban ghi thanh toan.";
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
            TempData["CheckoutMessage"] = "Thanh toan thanh cong. Cam on ban da dat hang tai HoaXinh Store.";
            TempData["CheckoutStatus"] = "success";

            if (!wasPaid)
            {
                await emailService.SendOrderPaymentSuccessAsync(order);
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
            return "Ket qua thanh toan khong hop le (sai chu ky). Vui long lien he ho tro.";
        }

        if (!amountValid)
        {
            return "So tien thanh toan khong khop don hang. Vui long lien he ho tro.";
        }

        return responseCode switch
        {
            "24" => "Ban da huy giao dich thanh toan.",
            "51" => "Tai khoan khong du so du de thanh toan.",
            "65" => "Tai khoan vuot qua han muc giao dich trong ngay.",
            "75" => "Ngan hang dang bao tri. Vui long thu lai sau.",
            _ => $"Thanh toan that bai (ma loi: {responseCode})."
        };
    }
}
