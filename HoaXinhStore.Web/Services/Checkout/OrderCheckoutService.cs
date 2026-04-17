using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Services.Checkout;

public class OrderCheckoutService(AppDbContext db, IInventoryService inventoryService) : IOrderCheckoutService
{
    public PaymentMethod ParsePaymentMethod(string? raw)
    {
        var normalized = (raw ?? "COD").Trim().ToUpperInvariant();
        return normalized switch
        {
            "VNPAY" => PaymentMethod.VNPAY,
            "QRPAY" => PaymentMethod.QRPAY,
            "AUTOQR" => PaymentMethod.AUTOQR,
            _ => PaymentMethod.COD
        };
    }

    public async Task<CheckoutProcessingResult> CreateOrderAsync(CheckoutRequestData request)
    {
        if (request.Items.Count == 0)
        {
            return new CheckoutProcessingResult
            {
                Success = false,
                ErrorMessage = "Vui lòng chọn ít nhất 1 sản phẩm."
            };
        }

        var paymentMethod = ParsePaymentMethod(request.PaymentMethodRaw);
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id);
        if (products.Count != productIds.Count)
        {
            return new CheckoutProcessingResult
            {
                Success = false,
                ErrorMessage = "Có sản phẩm không hợp lệ hoặc đã ngừng bán."
            };
        }

        foreach (var item in request.Items)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                return new CheckoutProcessingResult
                {
                    Success = false,
                    ErrorMessage = "Có sản phẩm không hợp lệ."
                };
            }
            if (item.VariantId.HasValue)
            {
                var validVariant = await db.ProductVariants.AnyAsync(v =>
                    v.Id == item.VariantId.Value && v.ProductId == item.ProductId && v.IsActive);
                if (!validVariant)
                {
                    return new CheckoutProcessingResult
                    {
                        Success = false,
                        ErrorMessage = "Biến thể sản phẩm không hợp lệ."
                    };
                }
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync();

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

        var activeVariants = await db.ProductVariants
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .ToListAsync();
        var variantsById = activeVariants.ToDictionary(v => v.Id, v => v);
        var defaultVariantByProductId = activeVariants
            .GroupBy(v => v.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsDefault).ThenBy(x => x.SortOrder).ThenBy(x => x.Id).FirstOrDefault());

        void SyncProductFromVariants(int productId)
        {
            var rows = activeVariants.Where(x => x.ProductId == productId && x.IsActive).ToList();
            if (rows.Count == 0) return;
            var availableTotal = rows.Sum(x => Math.Max(0, x.AvailableStock));
            if (products.TryGetValue(productId, out var productEntity))
            {
                productEntity.StockQuantity = availableTotal;
                productEntity.IsPreOrderEnabled = availableTotal <= 0;
            }
        }

        var orderItems = new List<OrderItem>();
        foreach (var i in request.Items)
        {
            var qty = Math.Max(1, i.Quantity);
            var unitFactor = Math.Max(1, i.UnitFactor);
            var product = products[i.ProductId];
            var variant = i.VariantId.HasValue && variantsById.TryGetValue(i.VariantId.Value, out var foundVariant)
                ? foundVariant
                : (defaultVariantByProductId.TryGetValue(i.ProductId, out var fallbackVariant) ? fallbackVariant : null);

            ProductVariant? inventoryVariant = variant;
            if (variant is not null)
            {
                if (paymentMethod == PaymentMethod.COD || paymentMethod == PaymentMethod.QRPAY)
                {
                    var availableNow = Math.Max(0, variant.AvailableStock);
                    if (qty > availableNow)
                    {
                        await tx.RollbackAsync();
                        return new CheckoutProcessingResult
                        {
                            Success = false,
                            ErrorMessage = $"Sản phẩm \"{product.Name}\" hiện không đủ tồn kho khả dụng.",
                            RedirectToCart = true
                        };
                    }
                    variant.StockQuantity = Math.Max(0, variant.StockQuantity - qty);
                    variant.AvailableStock = Math.Max(0, variant.StockQuantity - Math.Max(0, variant.ReservedStock));
                    SyncProductFromVariants(i.ProductId);
                }
                else
                {
                    var reserveOk = await inventoryService.TryReserveVariantAsync(variant.Id, qty);
                    if (!reserveOk)
                    {
                        await tx.RollbackAsync();
                        return new CheckoutProcessingResult
                        {
                            Success = false,
                            ErrorMessage = $"Sản phẩm \"{product.Name}\" hiện không đủ tồn kho khả dụng.",
                            RedirectToCart = true
                        };
                    }
                }
            }
            else
            {
                var available = Math.Max(0, product.StockQuantity);
                if (qty > available)
                {
                    await tx.RollbackAsync();
                    return new CheckoutProcessingResult
                    {
                        Success = false,
                        ErrorMessage = $"Sản phẩm \"{product.Name}\" hiện chỉ còn {available} trong kho.",
                        RedirectToCart = true
                    };
                }
                product.StockQuantity = Math.Max(0, available - qty);
            }

            var unitPrice = variant?.SalePrice ?? variant?.Price ?? product.SalePrice ?? product.Price;
            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                VariantId = inventoryVariant?.Id,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = variant?.Sku ?? product.Sku,
                VariantNameSnapshot = variant?.Name ?? string.Empty,
                Quantity = qty,
                UnitFactor = unitFactor,
                UnitName = i.UnitName ?? string.Empty,
                IsPreOrder = variant is not null ? variant.AvailableStock <= 0 : product.StockQuantity <= 0,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * qty
            });
        }

        var subtotal = orderItems.Sum(i => i.LineTotal);
        var order = new Order
        {
            OrderNo = $"HX{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(10, 99)}",
            CustomerId = customer.Id,
            OrderStatus = "PendingConfirm",
            PaymentStatus = paymentMethod == PaymentMethod.COD
                ? "Pending"
                : paymentMethod == PaymentMethod.QRPAY
                    ? "Paid"
                    : paymentMethod == PaymentMethod.AUTOQR
                        ? "AwaitingCustomerTransfer"
                        : "AwaitingGateway",
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
                    Provider = paymentMethod == PaymentMethod.VNPAY
                        ? "VNPAY"
                        : paymentMethod == PaymentMethod.QRPAY
                            ? "QRPAY"
                            : paymentMethod == PaymentMethod.AUTOQR
                                ? "AUTOQR"
                                : "COD",
                    PaymentMethod = paymentMethod.ToString(),
                    Amount = subtotal,
                    Status = paymentMethod == PaymentMethod.COD
                        ? "Pending"
                        : paymentMethod == PaymentMethod.QRPAY
                            ? "Paid"
                            : "Initiated",
                    PaidAtUtc = paymentMethod == PaymentMethod.QRPAY ? DateTime.UtcNow : null
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

        if (paymentMethod == PaymentMethod.COD || paymentMethod == PaymentMethod.QRPAY)
        {
            var touchedIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
            foreach (var pid in touchedIds)
            {
                SyncProductFromVariants(pid);
            }
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        return new CheckoutProcessingResult
        {
            Success = true,
            PaymentMethod = paymentMethod,
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            ShouldClearCartImmediately = paymentMethod == PaymentMethod.COD || paymentMethod == PaymentMethod.QRPAY
        };
    }
}
