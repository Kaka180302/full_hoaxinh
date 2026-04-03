using HoaXinhStore.Web.Entities;

namespace HoaXinhStore.Web.Services.Notifications;

public interface IEmailService
{
    Task SendOrderPlacedAsync(Order order, string? trackingUrl = null);
    Task SendOrderPaymentSuccessAsync(Order order, string? trackingUrl = null);
}
