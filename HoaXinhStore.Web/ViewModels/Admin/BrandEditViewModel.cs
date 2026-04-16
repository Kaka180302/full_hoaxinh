using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class BrandEditViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Tên thương hiệu")]
    [Required(ErrorMessage = "Vui lòng nhập tên thương hiệu.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Danh mục")]
    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Display(Name = "Hiển thị")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Ảnh thương hiệu")]
    public IFormFile? ImageFile { get; set; }

    public string? ImageUrl { get; set; }

    public List<SelectListItem> CategoryOptions { get; set; } = [];
}
