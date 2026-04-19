namespace HoaXinhStore.Web.ViewModels.Admin;

public class PaymentMethodSettingsViewModel
{
    public bool CodEnabled { get; set; } = true;
    public bool VnPayEnabled { get; set; } = true;
    public bool QrPayEnabled { get; set; } = true;
    public bool AutoQrEnabled { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}

