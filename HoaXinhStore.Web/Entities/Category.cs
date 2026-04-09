using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(30)]
    public string SkuPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<CategoryBrand> Brands { get; set; } = new List<CategoryBrand>();
}
