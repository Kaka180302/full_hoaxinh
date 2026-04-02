using HoaXinhStore.Web.Entities;

namespace HoaXinhStore.Web.Services.Notifications;

public interface IEmailService
{
    Task SendOrderPaymentSuccessAsync(Order order);
}
