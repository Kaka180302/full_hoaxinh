using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels;

public class CreateOrderRequest
{
    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string PaymentMethod { get; set; } = "COD";

    public bool IsExportInvoice { get; set; }
    public string VatCompanyName { get; set; } = string.Empty;
    public string VatTaxCode { get; set; } = string.Empty;
    public string VatCompanyAddress { get; set; } = string.Empty;
    public string VatEmail { get; set; } = string.Empty;

    [Required]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
