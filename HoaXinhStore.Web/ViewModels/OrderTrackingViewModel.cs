namespace HoaXinhStore.Web.ViewModels;

public class OrderTrackingViewModel
{
    public string OrderNo { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool HasSearched { get; set; }
    public bool Found { get; set; }
    public string Message { get; set; } = string.Empty;
    public OrderTrackingResult? Order { get; set; }
}

public class OrderTrackingResult
{
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string ShippingCarrier { get; set; } = string.Empty;
    public string ShippingNote { get; set; } = string.Empty;
}
