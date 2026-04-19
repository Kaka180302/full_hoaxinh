namespace HoaXinhStore.Web.Services.Payments;

public class PaymentMethodSettings
{
    public bool CodEnabled { get; set; } = true;
    public bool VnPayEnabled { get; set; } = true;
    public bool QrPayEnabled { get; set; } = true;
    public bool AutoQrEnabled { get; set; } = true;

    public HashSet<string> GetEnabledMethodSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CodEnabled) set.Add("COD");
        if (VnPayEnabled) set.Add("VNPAY");
        if (QrPayEnabled) set.Add("QRPAY");
        if (AutoQrEnabled) set.Add("AUTOQR");
        return set;
    }
}

