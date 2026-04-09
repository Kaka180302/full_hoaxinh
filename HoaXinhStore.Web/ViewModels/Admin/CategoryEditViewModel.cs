using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class CategoryEditViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Tên danh mục")]
    [Required(ErrorMessage = "Vui lòng nhập tên danh mục."), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Slug")]
    [Required(ErrorMessage = "Vui lòng nhập slug."), StringLength(120)]
    public string Slug { get; set; } = string.Empty;

    [Display(Name = "Mã SKU danh mục")]
    [StringLength(30)]
    public string SkuPrefix { get; set; } = string.Empty;

    [Display(Name = "Hiển thị danh mục")]
    public bool IsActive { get; set; } = true;
}
