using HoaXinhStore.Web.Entities;

namespace HoaXinhStore.Web.Services.Payments;

public interface IVnpayService
{
    string BuildPaymentUrl(Order order, string clientIp, string? bankCode = null, string? returnUrlOverride = null);
    bool IsValidSignature(IQueryCollection query);
}
