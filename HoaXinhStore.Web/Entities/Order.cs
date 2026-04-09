using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HoaXinhStore.Web.Entities;

public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string OrderNo { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required, MaxLength(30)]
    public string OrderStatus { get; set; } = "PendingConfirm";

    [Required, MaxLength(30)]
    public string PaymentStatus { get; set; } = "Pending";

    public PaymentMethod PaymentMethod { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(500)]
    public string Note { get; set; } = string.Empty;

    public bool IsExportInvoice { get; set; }

    [MaxLength(200)]
    public string VatCompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string VatTaxCode { get; set; } = string.Empty;

    [MaxLength(300)]
    public string VatCompanyAddress { get; set; } = string.Empty;

    [MaxLength(100)]
    public string VatEmail { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<OrderTimeline> Timelines { get; set; } = new List<OrderTimeline>();
}
