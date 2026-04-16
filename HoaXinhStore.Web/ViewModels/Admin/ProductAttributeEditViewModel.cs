using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class ProductAttributeEditViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên thuộc tính.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public List<ProductAttributeValueEditItem> Values { get; set; } = [];
}

public class ProductAttributeValueEditItem
{
    public int? Id { get; set; }

    [StringLength(120)]
    public string Value { get; set; } = string.Empty;

    [Display(Name = "Hệ số quy đổi")]
    [Range(0, 9999999, ErrorMessage = "Hệ số phải >= 0")]
    public int? ConversionFactor { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
