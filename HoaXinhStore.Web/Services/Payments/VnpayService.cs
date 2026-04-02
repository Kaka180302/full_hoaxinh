using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Options;
using Microsoft.Extensions.Options;

namespace HoaXinhStore.Web.Services.Payments;

public class VnpayService(IOptions<VnpayOptions> options) : IVnpayService
{
    private readonly VnpayOptions _options = options.Value;

    public string BuildPaymentUrl(Order order, string clientIp, string? bankCode = null, string? returnUrlOverride = null)
    {
        var now = DateTime.UtcNow.AddHours(7);
        var txnRef = order.OrderNo;
        var amount = (long)Math.Round(order.TotalAmount * 100m, MidpointRounding.AwayFromZero);
        var orderInfo = BuildOrderInfo(order.OrderNo);

        var inputData = new SortedDictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = string.IsNullOrWhiteSpace(returnUrlOverride) ? _options.ReturnUrl : returnUrlOverride,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss")
        };

        if (!string.IsNullOrWhiteSpace(bankCode))
        {
            inputData["vnp_BankCode"] = bankCode;
        }

        var queryString = string.Join("&", inputData.Select(kv => $"{kv.Key}={VnpUrlEncode(kv.Value)}"));
        var secureHash = ComputeHmacSha512(_options.HashSecret, queryString);
        return $"{_options.BaseUrl}?{queryString}&vnp_SecureHashType=HmacSHA512&vnp_SecureHash={secureHash}";
    }

    public bool IsValidSignature(IQueryCollection query)
    {
        var responseData = query
            .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                         && !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                         && !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var rawData = string.Join("&", responseData
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={VnpUrlEncode(kv.Value)}"));

        var expected = ComputeHmacSha512(_options.HashSecret, rawData);
        var actual = query["vnp_SecureHash"].ToString();

        return !string.IsNullOrWhiteSpace(actual)
               && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHmacSha512(string key, string inputData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string VnpUrlEncode(string value)
    {
        return WebUtility.UrlEncode(value ?? string.Empty);
    }

    private static string BuildOrderInfo(string orderNo)
    {
        var normalized = $"Thanh toan don hang {orderNo}".Trim();
        return Regex.Replace(normalized, @"[^a-zA-Z0-9\s\-\.]", string.Empty);
    }
}
