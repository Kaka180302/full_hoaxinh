namespace HoaXinhStore.Web.Options;

public class OrderPaymentTimeoutOptions
{
    public bool Enabled { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 15;
    public int SweepIntervalSeconds { get; set; } = 60;
}
