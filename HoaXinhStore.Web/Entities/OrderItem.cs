using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HoaXinhStore.Web.Entities;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? VariantId { get; set; }

    [Required, MaxLength(255)]
    public string ProductNameSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string SkuSnapshot { get; set; } = string.Empty;

    [MaxLength(120)]
    public string VariantNameSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int UnitFactor { get; set; } = 1;

    [MaxLength(80)]
    public string UnitName { get; set; } = string.Empty;

    public bool IsPreOrder { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}
