using HoaXinhStore.Web.Entities;

namespace HoaXinhStore.Web.Services.Notifications;

public interface IEmailService
{
    Task SendOrderPlacedAsync(Order order, string? trackingUrl = null);
    Task SendOrderPaymentSuccessAsync(Order order, string? trackingUrl = null);
    Task SendPreOrderRequestAsync(string productName, string sku, int requestedQty, string customerName, string phone, string email, string address, string note = "");
}
