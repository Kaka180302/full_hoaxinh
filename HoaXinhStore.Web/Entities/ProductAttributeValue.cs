using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class ProductAttributeValue
{
    public int Id { get; set; }
    public int ProductAttributeId { get; set; }
    public ProductAttribute? ProductAttribute { get; set; }

    [Required, MaxLength(120)]
    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

