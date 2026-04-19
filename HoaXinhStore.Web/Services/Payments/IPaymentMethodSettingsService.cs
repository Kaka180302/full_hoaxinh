namespace HoaXinhStore.Web.Services.Payments;

public interface IPaymentMethodSettingsService
{
    Task<PaymentMethodSettings> GetAsync();
    Task SaveAsync(PaymentMethodSettings settings);
}

