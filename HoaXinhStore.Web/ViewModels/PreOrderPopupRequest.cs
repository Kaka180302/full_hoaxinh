namespace HoaXinhStore.Web.ViewModels;

public class PreOrderPopupRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
}
