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

    [Display(Name = "Tồn kho")]
    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho phải lớn hơn hoặc bằng 0.")]
    public int StockQuantity { get; set; }

    [Display(Name = "Ảnh sản phẩm (URL)")]
    [StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [Display(Name = "Mô tả ngắn")]
    [StringLength(1000)]
    public string Summary { get; set; } = string.Empty;

    [Display(Name = "Mô tả chi tiết")]
    public string Descriptions { get; set; } = string.Empty;

    [Display(Name = "Danh mục")]
    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Display(Name = "Hiển thị sản phẩm")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Tải ảnh từ máy")]
    public IFormFile? ImageFile { get; set; }

    public List<SelectListItem> CategoryOptions { get; set; } = [];
}
