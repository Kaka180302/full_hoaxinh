using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HoaXinhStore.Web.Entities;

public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, MaxLength(30)]
    public string Provider { get; set; } = "COD";

    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = "COD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending";

    [MaxLength(100)]
    public string TransactionRef { get; set; } = string.Empty;

    public DateTime? PaidAtUtc { get; set; }

    public string RawResponseJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
