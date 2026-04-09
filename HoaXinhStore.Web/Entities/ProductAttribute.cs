using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class ProductAttribute
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<ProductAttributeValue> Values { get; set; } = new List<ProductAttributeValue>();
}

