using Microsoft.AspNetCore.Mvc;

namespace HoaXinhStore.Web.Controllers.Api;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    [HttpGet("create-url")]
    public IActionResult CreatePaymentUrl([FromQuery] int orderId)
    {
        // TODO: Integrate real VNPay gateway.
        var fallbackUrl = Url.Action("Index", "Store", null, Request.Scheme) ?? "/";
        return Content(fallbackUrl);
    }
}
