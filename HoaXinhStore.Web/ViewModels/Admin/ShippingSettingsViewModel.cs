namespace HoaXinhStore.Web.ViewModels.Admin;

public class ShippingSettingsViewModel
{
    public bool GhnEnabled { get; set; } = true;
    public string ProviderName { get; set; } = "GHN";
    public string WebhookKey { get; set; } = string.Empty;
    public string DefaultCarrierDisplayName { get; set; } = "GHN";
    public bool AutoUpdateOrderStatus { get; set; } = true;
    public bool AutoMarkCodPaidWhenDelivered { get; set; } = true;
    public string Note { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

