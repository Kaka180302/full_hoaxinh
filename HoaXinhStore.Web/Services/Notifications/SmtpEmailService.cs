using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Options;
using Microsoft.Extensions.Options;

namespace HoaXinhStore.Web.Services.Notifications;

public class SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _smtp = options.Value;
    private readonly ILogger<SmtpEmailService> _logger = logger;

    public async Task SendOrderPlacedAsync(Order order, string? trackingUrl = null)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.FromEmail))
        {
            return;
        }

        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var customerName = order.Customer?.FullName ?? "Quý khách";
        var customerPhone = order.Customer?.Phone ?? "";
        var customerEmail = order.Customer?.Email ?? "";
        var customerAddress = order.Customer?.Addresses?.FirstOrDefault(a => a.IsDefault)?.AddressLine
                              ?? order.Customer?.Addresses?.FirstOrDefault()?.AddressLine
                              ?? "Chưa có thông tin địa chỉ";
        var companyEmail = string.IsNullOrWhiteSpace(_smtp.CompanyNotificationEmail)
            ? "Infor@hoaxinhgroup.vn"
            : _smtp.CompanyNotificationEmail.Trim();

        var customerBody = BuildOrderPlacedCustomerMailBody(order, customerName, customerPhone, customerEmail, vi, trackingUrl);
        var companyBody = BuildCompanyMailBody(order, customerName, customerPhone, customerEmail, customerAddress, vi);
        var customerSubject = $"[HoaXinh Store] Đã tiếp nhận đơn hàng - {order.OrderNo}";
        var companySubject = $"[HoaXinh Store] Đơn hàng mới từ HoaXinhStore - {order.OrderNo}";

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            await SendEmailAsync(customerEmail, customerSubject, customerBody, order.OrderNo);
        }

        if (!string.IsNullOrWhiteSpace(companyEmail))
        {
            await SendEmailAsync(companyEmail, companySubject, companyBody, order.OrderNo);
        }
    }

    public async Task SendOrderPaymentSuccessAsync(Order order, string? trackingUrl = null)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.FromEmail))
        {
            return;
        }

        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var customerName = order.Customer?.FullName ?? "Quý khách";
        var customerPhone = order.Customer?.Phone ?? "";
        var customerEmail = order.Customer?.Email ?? "";
        var customerAddress = order.Customer?.Addresses?.FirstOrDefault(a => a.IsDefault)?.AddressLine
                              ?? order.Customer?.Addresses?.FirstOrDefault()?.AddressLine
                              ?? "Chưa có thông tin địa chỉ";
        var companyEmail = string.IsNullOrWhiteSpace(_smtp.CompanyNotificationEmail)
            ? "Infor@hoaxinhgroup.vn"
            : _smtp.CompanyNotificationEmail.Trim();

        var customerBody = BuildCustomerMailBody(order, customerName, customerPhone, customerEmail, vi, trackingUrl);
        var companyBody = BuildCompanyMailBody(order, customerName, customerPhone, customerEmail, customerAddress, vi);
        var customerSubject = $"[HoaXinh Store] Xác nhận thanh toán thành công - {order.OrderNo}";
        var companySubject = $"[HoaXinh Store] Đơn hàng mới từ HoaXinhStore - {order.OrderNo}";

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            await SendEmailAsync(customerEmail, customerSubject, customerBody, order.OrderNo);
        }

        if (!string.IsNullOrWhiteSpace(companyEmail))
        {
            await SendEmailAsync(companyEmail, companySubject, companyBody, order.OrderNo);
        }
    }

    public async Task SendPreOrderRequestAsync(string productName, string sku, int requestedQty, string customerName, string phone, string email, string address, string note = "")
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.FromEmail))
        {
            return;
        }

        var companyEmail = string.IsNullOrWhiteSpace(_smtp.CompanyNotificationEmail)
            ? "Infor@hoaxinhgroup.vn"
            : _smtp.CompanyNotificationEmail.Trim();
        if (string.IsNullOrWhiteSpace(companyEmail))
        {
            return;
        }

        var subject = $"[HoaXinh Store] Yêu cầu đặt trước - {sku}";
        var body = $"""
<div style='font-family:Arial,sans-serif;line-height:1.6'>
  <h2>Yêu cầu đặt trước mới</h2>
  <p><b>Sản phẩm:</b> {WebUtility.HtmlEncode(productName)}</p>
  <p><b>SKU:</b> {WebUtility.HtmlEncode(sku)}</p>
  <p><b>Số lượng khách yêu cầu:</b> {requestedQty}</p>
  <p><b>Khách hàng:</b> {WebUtility.HtmlEncode(customerName)}</p>
  <p><b>SĐT:</b> {WebUtility.HtmlEncode(phone)}</p>
  <p><b>Email:</b> {WebUtility.HtmlEncode(email)}</p>
  <p><b>Địa chỉ:</b> {WebUtility.HtmlEncode(address)}</p>
  <p><b>Ghi chú:</b> {WebUtility.HtmlEncode(note)}</p>
</div>
""";
        await SendEmailAsync(companyEmail, subject, body, sku);
    }

    private static string BuildOrderPlacedCustomerMailBody(
        Order order,
        string customerName,
        string customerPhone,
        string customerEmail,
        CultureInfo vi,
        string? trackingUrl)
    {
        var paymentText = string.Equals(order.PaymentMethod.ToString(), "COD", StringComparison.OrdinalIgnoreCase)
            ? "Đơn hàng COD của bạn đã được tiếp nhận."
            : "Đơn hàng của bạn đã được tạo thành công và đang chờ thanh toán online.";

        var sb = new StringBuilder();
        sb.AppendLine("<div style='font-family:Arial,sans-serif;line-height:1.6'>");
        sb.AppendLine("<h2>HoaXinh Store - Xác nhận tiếp nhận đơn hàng</h2>");
        sb.AppendLine($"<p>Xin chào <b>{WebUtility.HtmlEncode(customerName)}</b>,</p>");
        sb.AppendLine($"<p>{paymentText}</p>");
        sb.AppendLine($"<p><b>Mã đơn:</b> {WebUtility.HtmlEncode(order.OrderNo)}</p>");
        sb.AppendLine($"<p><b>Khách hàng:</b> {WebUtility.HtmlEncode(customerName)} - {WebUtility.HtmlEncode(customerPhone)} - {WebUtility.HtmlEncode(customerEmail)}</p>");
        sb.AppendLine("<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%'>");
        sb.AppendLine("<thead><tr><th>Sản phẩm</th><th>SL</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead><tbody>");

        foreach (var item in order.Items)
        {
            sb.AppendLine($"<tr><td>{WebUtility.HtmlEncode(item.ProductNameSnapshot)}</td><td>{item.Quantity}</td><td>{item.UnitPrice.ToString("N0", vi)} đ</td><td>{item.LineTotal.ToString("N0", vi)} đ</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p><b>Tổng tiền:</b> {order.TotalAmount.ToString("N0", vi)} đ</p>");
        if (!string.IsNullOrWhiteSpace(trackingUrl))
        {
            var safeUrl = WebUtility.HtmlEncode(trackingUrl);
            sb.AppendLine("<p><b>Theo dõi đơn hàng:</b></p>");
            sb.AppendLine($"<p><a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener noreferrer\">{safeUrl}</a></p>");
        }
        sb.AppendLine("<p>HoaXinh Store trân trọng cảm ơn bạn đã tin tưởng và đồng hành.</p>");
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private async Task SendEmailAsync(string to, string subject, string body, string orderNo)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        mail.To.Add(to);

        using var client = new SmtpClient(_smtp.Host, _smtp.Port)
        {
            UseDefaultCredentials = false,
            EnableSsl = _smtp.EnableSsl,
            Credentials = new NetworkCredential(_smtp.Username, _smtp.Password)
        };

        try
        {
            await client.SendMailAsync(mail);
            _logger.LogInformation("Send mail success for order {OrderNo} to {To}", orderNo, to);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send mail failed for order {OrderNo} to {To}", orderNo, to);
        }
    }

    private static string BuildCustomerMailBody(
        Order order,
        string customerName,
        string customerPhone,
        string customerEmail,
        CultureInfo vi,
        string? trackingUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div style='font-family:Arial,sans-serif;line-height:1.6'>");
        sb.AppendLine("<h2>HoaXinh Store - Xác nhận thanh toán thành công</h2>");
        sb.AppendLine($"<p>Xin chào <b>{WebUtility.HtmlEncode(customerName)}</b>,</p>");
        sb.AppendLine("<p>Cảm ơn bạn đã đặt hàng tại HoaXinh Store. Đơn hàng của bạn đã được thanh toán thành công.</p>");
        sb.AppendLine($"<p><b>Mã đơn:</b> {WebUtility.HtmlEncode(order.OrderNo)}</p>");
        sb.AppendLine($"<p><b>Khách hàng:</b> {WebUtility.HtmlEncode(customerName)} - {WebUtility.HtmlEncode(customerPhone)} - {WebUtility.HtmlEncode(customerEmail)}</p>");
        sb.AppendLine("<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%'>");
        sb.AppendLine("<thead><tr><th>Sản phẩm</th><th>SL</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead><tbody>");

        foreach (var item in order.Items)
        {
            sb.AppendLine($"<tr><td>{WebUtility.HtmlEncode(item.ProductNameSnapshot)}</td><td>{item.Quantity}</td><td>{item.UnitPrice.ToString("N0", vi)} đ</td><td>{item.LineTotal.ToString("N0", vi)} đ</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p><b>Tổng tiền:</b> {order.TotalAmount.ToString("N0", vi)} đ</p>");
        if (!string.IsNullOrWhiteSpace(trackingUrl))
        {
            var safeUrl = WebUtility.HtmlEncode(trackingUrl);
            sb.AppendLine("<p><b>Theo dõi đơn hàng:</b></p>");
            sb.AppendLine($"<p><a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener noreferrer\">{safeUrl}</a></p>");
        }
        sb.AppendLine("<p>HoaXinh Store trân trọng cảm ơn bạn đã tin tưởng và đồng hành.</p>");
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string BuildCompanyMailBody(
        Order order,
        string customerName,
        string customerPhone,
        string customerEmail,
        string customerAddress,
        CultureInfo vi)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div style='margin:0;padding:24px;background:#f5f6f8;font-family:Arial,sans-serif;color:#1f2937'>");
        sb.AppendLine("<div style='max-width:900px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb'>");
        sb.AppendLine("<div style='padding:28px 28px 20px;border-bottom:1px solid #e5e7eb;text-align:center'>");
        sb.AppendLine("<h1 style='margin:0;color:#9a6b3d;font-size:40px;line-height:1.2'>Đơn hàng mới từ HoaXinhStore</h1>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div style='padding:24px 28px'>");
        sb.AppendLine("<div style='border-bottom:1px solid #d1d5db;padding-bottom:16px;margin-bottom:16px'>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>Mã đơn:</strong> {WebUtility.HtmlEncode(order.OrderNo)}</p>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>Tên khách:</strong> {WebUtility.HtmlEncode(customerName)}</p>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>SĐT:</strong> {WebUtility.HtmlEncode(customerPhone)}</p>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>Email:</strong> {WebUtility.HtmlEncode(customerEmail)}</p>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>Địa chỉ:</strong> {WebUtility.HtmlEncode(customerAddress)}</p>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>Phương thức thanh toán:</strong> {ToPaymentMethodVi(order.PaymentMethod)}</p>");
        sb.AppendLine($"<p style='margin:8px 0'><strong>Hóa đơn VAT:</strong> {(order.IsExportInvoice ? "Có" : "Không")}</p>");

        if (order.IsExportInvoice)
        {
            sb.AppendLine("<div style='margin-top:10px;padding:12px;border:1px solid #f3e8d6;background:#fffaf3'>");
            sb.AppendLine("<p style='margin:6px 0'><strong>Thông tin xuất hóa đơn VAT</strong></p>");
            sb.AppendLine($"<p style='margin:6px 0'><strong>Tên công ty:</strong> {WebUtility.HtmlEncode(order.VatCompanyName)}</p>");
            sb.AppendLine($"<p style='margin:6px 0'><strong>Mã số thuế:</strong> {WebUtility.HtmlEncode(order.VatTaxCode)}</p>");
            sb.AppendLine($"<p style='margin:6px 0'><strong>Địa chỉ công ty:</strong> {WebUtility.HtmlEncode(order.VatCompanyAddress)}</p>");
            sb.AppendLine($"<p style='margin:6px 0'><strong>Email nhận hóa đơn:</strong> {WebUtility.HtmlEncode(order.VatEmail)}</p>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");
        sb.AppendLine("<h3 style='margin:0 0 12px;font-size:28px;color:#111827'>Sản phẩm</h3>");
        sb.AppendLine("<table style='width:100%;border-collapse:collapse;border:1px solid #d1d5db'>");
        sb.AppendLine("<thead><tr style='background:#f9fafb'><th style='border:1px solid #d1d5db;padding:12px;text-align:left'>Tên sản phẩm</th><th style='border:1px solid #d1d5db;padding:12px;text-align:right'>Giá</th><th style='border:1px solid #d1d5db;padding:12px;text-align:center'>SL</th><th style='border:1px solid #d1d5db;padding:12px;text-align:right'>Thành tiền</th></tr></thead><tbody>");
        foreach (var item in order.Items)
        {
            sb.AppendLine($"<tr><td style='border:1px solid #d1d5db;padding:12px'>{WebUtility.HtmlEncode(item.ProductNameSnapshot)}</td><td style='border:1px solid #d1d5db;padding:12px;text-align:right'>{item.UnitPrice.ToString("N0", vi)} đ</td><td style='border:1px solid #d1d5db;padding:12px;text-align:center'>{item.Quantity}</td><td style='border:1px solid #d1d5db;padding:12px;text-align:right'>{item.LineTotal.ToString("N0", vi)} đ</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p style='margin:20px 0 0;font-size:32px;color:#9a6b3d'><strong>Tổng tiền: {order.TotalAmount.ToString("N0", vi)} VND</strong></p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div style='padding:18px 28px;border-top:1px solid #e5e7eb;color:#6b7280;text-align:center'>Email được gửi tự động từ hệ thống đặt hàng</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string ToPaymentMethodVi(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.COD => "Thanh toán khi nhận hàng (COD)",
            PaymentMethod.VNPAY => "Thanh toán online (VNPAY)",
            _ => method.ToString()
        };
    }
}
