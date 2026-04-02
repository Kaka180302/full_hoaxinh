using HoaXinhStore.Web.Services.Policies;

namespace HoaXinhStore.Web.ViewModels;

public class StoreIndexViewModel
{
    public List<StoreCategoryViewModel> Categories { get; set; } = [];
    public List<StoreProductViewModel> Products { get; set; } = [];
    public Dictionary<string, PolicyContentItem> PolicyData { get; set; } = [];
}

public class StoreCategoryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class StoreProductViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = "all";
}
