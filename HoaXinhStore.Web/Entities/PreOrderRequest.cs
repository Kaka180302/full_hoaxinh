using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HoaXinhStore.Web.Entities;

public class PreOrderRequest
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, MaxLength(255)]
    public string ProductNameSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ProductSkuSnapshot { get; set; } = string.Empty;

    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int MissingQuantity { get; set; }

    [Required, MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    public decimal DepositPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPriceSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PreOrderAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DepositAmount { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
