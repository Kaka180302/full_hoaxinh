using HoaXinhStore.Web.Entities;

namespace HoaXinhStore.Web.Services.Checkout;

public interface IOrderCheckoutService
{
    PaymentMethod ParsePaymentMethod(string? raw);
    Task<CheckoutProcessingResult> CreateOrderAsync(CheckoutRequestData request);
}

public sealed class CheckoutRequestData
{
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PaymentMethodRaw { get; set; } = "COD";
    public bool IsExportInvoice { get; set; }
    public string VatCompanyName { get; set; } = string.Empty;
    public string VatTaxCode { get; set; } = string.Empty;
    public string VatCompanyAddress { get; set; } = string.Empty;
    public string VatEmail { get; set; } = string.Empty;
    public List<CheckoutItemData> Items { get; set; } = [];
}

public sealed class CheckoutItemData
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int Quantity { get; set; }
    public int UnitFactor { get; set; } = 1;
    public string UnitName { get; set; } = string.Empty;
}

public sealed class CheckoutProcessingResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool RedirectToCart { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public bool ShouldClearCartImmediately { get; set; }
}
