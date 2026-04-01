using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HoaXinhStore.Web.Entities;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public int StockQuantity { get; set; }

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Summary { get; set; } = string.Empty;

    public string Descriptions { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
