using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class CategoryEditViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
