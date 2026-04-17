using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Services.Checkout;
using HoaXinhStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HoaXinhStore.Web.Controllers.Api;

[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderCheckoutService orderCheckoutService) : ControllerBase
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
            Items = request.Items.Select(i => new CheckoutItemData
            {
                ProductId = i.ProductId,
                VariantId = null,
                Quantity = i.Quantity,
                UnitFactor = 1,
                UnitName = string.Empty
            }).ToList()
        });

        if (!checkoutResult.Success)
        {
            return BadRequest(checkoutResult.ErrorMessage ?? "Unable to create order.");
        }

        return Ok(new { id = checkoutResult.OrderId, orderNo = checkoutResult.OrderNo });
    }
}
