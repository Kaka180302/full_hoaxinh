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
        sb.AppendLine("<h2>HoaXinh Store - X�c nh?n thanh to�n th�nh c�ng</h2>");
        sb.AppendLine($"<p>Xin ch�o <b>{WebUtility.HtmlEncode(order.Customer.FullName)}</b>,</p>");
        sb.AppendLine("<p>HoaXinh c?m on b?n d� d?t h�ng. �on h�ng c?a b?n d� du?c thanh to�n th�nh c�ng.</p>");
        sb.AppendLine($"<p><b>M� don:</b> {WebUtility.HtmlEncode(order.OrderNo)}</p>");
        sb.AppendLine($"<p><b>Kh�ch h�ng:</b> {WebUtility.HtmlEncode(order.Customer.FullName)} - {WebUtility.HtmlEncode(order.Customer.Phone)} - {WebUtility.HtmlEncode(order.Customer.Email)}</p>");
        sb.AppendLine("<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%'>");
        sb.AppendLine("<thead><tr><th>S?n ph?m</th><th>SL</th><th>�on gi�</th><th>Th�nh ti?n</th></tr></thead><tbody>");

        foreach (var item in order.Items)
        {
            sb.AppendLine($"<tr><td>{WebUtility.HtmlEncode(item.ProductNameSnapshot)}</td><td>{item.Quantity}</td><td>{item.UnitPrice.ToString("N0", vi)} d</td><td>{item.LineTotal.ToString("N0", vi)} d</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p><b>T?ng ti?n:</b> {order.TotalAmount.ToString("N0", vi)} d</p>");
        sb.AppendLine("<p>HoaXinh Store tr�n tr?ng c?m on b?n d� tin tu?ng v� d?ng h�nh.</p>");
        sb.AppendLine("</div>");

        using var mail = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = $"[HoaXinh] X�c nh?n thanh to�n th�nh c�ng - {order.OrderNo}",
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
