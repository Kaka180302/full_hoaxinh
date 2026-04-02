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

    public async Task SendOrderPaymentSuccessAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.Customer?.Email) || string.IsNullOrWhiteSpace(_smtp.Host))
        {
            return;
        }

        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var sb = new StringBuilder();

        sb.AppendLine("<div style='font-family:Arial,sans-serif;line-height:1.6'>");
        sb.AppendLine("<h2>HoaXinh Store - Xác nh?n thanh toán thành công</h2>");
        sb.AppendLine($"<p>Xin chào <b>{WebUtility.HtmlEncode(order.Customer.FullName)}</b>,</p>");
        sb.AppendLine("<p>HoaXinh c?m on b?n dã d?t hàng. Ðon hàng c?a b?n dã du?c thanh toán thành công.</p>");
        sb.AppendLine($"<p><b>Mã don:</b> {WebUtility.HtmlEncode(order.OrderNo)}</p>");
        sb.AppendLine($"<p><b>Khách hàng:</b> {WebUtility.HtmlEncode(order.Customer.FullName)} - {WebUtility.HtmlEncode(order.Customer.Phone)} - {WebUtility.HtmlEncode(order.Customer.Email)}</p>");
        sb.AppendLine("<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%'>");
        sb.AppendLine("<thead><tr><th>S?n ph?m</th><th>SL</th><th>Ðon giá</th><th>Thành ti?n</th></tr></thead><tbody>");

        foreach (var item in order.Items)
        {
            sb.AppendLine($"<tr><td>{WebUtility.HtmlEncode(item.ProductNameSnapshot)}</td><td>{item.Quantity}</td><td>{item.UnitPrice.ToString("N0", vi)} d</td><td>{item.LineTotal.ToString("N0", vi)} d</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p><b>T?ng ti?n:</b> {order.TotalAmount.ToString("N0", vi)} d</p>");
        sb.AppendLine("<p>HoaXinh Store trân tr?ng c?m on b?n dã tin tu?ng và d?ng hành.</p>");
        sb.AppendLine("</div>");

        using var mail = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = $"[HoaXinh] Xác nh?n thanh toán thành công - {order.OrderNo}",
            Body = sb.ToString(),
            IsBodyHtml = true
        };

        mail.To.Add(order.Customer.Email);

        using var client = new SmtpClient(_smtp.Host, _smtp.Port)
        {
            EnableSsl = _smtp.EnableSsl,
            Credentials = new NetworkCredential(_smtp.Username, _smtp.Password)
        };

        try
        {
            await client.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendOrderPaymentSuccessAsync failed for order {OrderNo}", order.OrderNo);
        }
    }
}
