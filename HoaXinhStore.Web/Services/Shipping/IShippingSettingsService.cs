namespace HoaXinhStore.Web.Services.Shipping;

public interface IShippingSettingsService
{
    Task<ShippingSettings> GetAsync();
    Task SaveAsync(ShippingSettings settings);
}

