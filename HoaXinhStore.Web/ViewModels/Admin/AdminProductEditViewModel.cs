using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class AdminProductEditViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Summary { get; set; } = string.Empty;

    public string Descriptions { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    public IFormFile? ImageFile { get; set; }

    public List<SelectListItem> CategoryOptions { get; set; } = [];
}
