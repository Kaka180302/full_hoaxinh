using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Controllers.Api;

[ApiController]
[Route("api/orders")]
public class OrdersController(AppDbContext db) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Items.Count == 0)
        {
            return BadRequest("Order must have at least one item.");
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != productIds.Count)
        {
            return BadRequest("One or more products are invalid.");
        }

        var paymentMethod = request.PaymentMethod.Equals("VNPAY", StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.VNPAY
            : PaymentMethod.COD;

        foreach (var item in request.Items)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                return BadRequest("One or more products are invalid.");
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
            product.StockQuantity = Math.Max(0, product.StockQuantity - qty);

            return new OrderItem
            {
                ProductId = product.Id,
                VariantId = null,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = product.Sku,
                VariantNameSnapshot = string.Empty,
                Quantity = qty,
                IsPreOrder = product.StockQuantity == 0,
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
            VatCompanyName = request.VatCompanyName,
            VatTaxCode = request.VatTaxCode,
            VatCompanyAddress = request.VatCompanyAddress,
            VatEmail = request.VatEmail,
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
            ToStatus = order.OrderStatus,
            Note = "Tạo đơn từ API"
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { id = order.Id, orderNo = order.OrderNo });
    }
}
