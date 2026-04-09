using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class AdminProductEditViewModel
{
    [Display(Name = "ID")]
    public int? Id { get; set; }

    [Display(Name = "Mã SKU")]
    [Required(ErrorMessage = "Vui lòng nhập mã SKU."), StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Display(Name = "Tên sản phẩm")]
    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm."), StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Giá bán")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0.")]
    public decimal Price { get; set; }

    [Display(Name = "Giá khuyến mãi")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá khuyến mãi phải lớn hơn hoặc bằng 0.")]
    public decimal? SalePrice { get; set; }

    [Display(Name = "Tồn kho")]
    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho phải lớn hơn hoặc bằng 0.")]
    public int StockQuantity { get; set; }

    [Display(Name = "Tồn kho (thùng)")]
    public int StockCase { get; set; }

    [Display(Name = "Tồn kho (lốc)")]
    public int StockPack { get; set; }

    public int CaseFactor { get; set; } = 0;
    public int PackFactor { get; set; } = 0;

    [Display(Name = "Mô tả ngắn")]
    [StringLength(1000)]
    public string Summary { get; set; } = string.Empty;

    [Display(Name = "Mô tả chi tiết")]
    public string Descriptions { get; set; } = string.Empty;

    [Display(Name = "Thông số kỹ thuật")]
    public string TechnicalSpecs { get; set; } = string.Empty;

    [Display(Name = "Hướng dẫn sử dụng")]
    public string UsageGuide { get; set; } = string.Empty;

    [Display(Name = "Danh mục")]
    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Display(Name = "Hiển thị sản phẩm")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Tải ảnh từ máy")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Thương hiệu")]
    public int? BrandId { get; set; }

    [Display(Name = "Ảnh con (nhiều ảnh)")]
    public List<IFormFile> GalleryFiles { get; set; } = [];
    public List<IFormFile?> VariantImageFiles { get; set; } = [];

    public List<SelectListItem> CategoryOptions { get; set; } = [];
    public List<SelectListItem> BrandOptions { get; set; } = [];
    public List<AdminProductUnitOptionInput> UnitOptions { get; set; } = [];
    public List<AdminProductVariantInput> Variants { get; set; } = [];
}

public class AdminProductUnitOptionInput
{
    [Display(Name = "Tên đơn vị")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Số lượng quy đổi")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public int Factor { get; set; } = 1;
}

public class AdminProductVariantInput
{
    public int? Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? Barcode { get; set; }
    public int? WeightGram { get; set; }
    public int? LengthMm { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
