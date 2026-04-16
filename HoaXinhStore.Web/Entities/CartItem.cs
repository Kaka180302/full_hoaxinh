using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class CartItem
{
    public int Id { get; set; }

    public int CartId { get; set; }
    public Cart? Cart { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? VariantId { get; set; }
    public ProductVariant? Variant { get; set; }

    public int Quantity { get; set; } = 1;
    public bool Checked { get; set; } = true;

    [MaxLength(120)]
    public string UnitName { get; set; } = string.Empty;

    public int UnitFactor { get; set; } = 1;
}

