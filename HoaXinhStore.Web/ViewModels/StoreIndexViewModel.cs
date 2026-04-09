using HoaXinhStore.Web.Services;
using HoaXinhStore.Web.Services.Policies;

namespace HoaXinhStore.Web.ViewModels;

public class StoreIndexViewModel
{
    public List<StoreCategoryViewModel> Categories { get; set; } = [];
    public List<StoreProductViewModel> Products { get; set; } = [];
    public List<StoreProductViewModel> FeaturedProducts { get; set; } = [];
    public List<CategorySectionViewModel> CategorySections { get; set; } = [];
    public Dictionary<string, PolicyContentItem> PolicyData { get; set; } = [];
}

public class CategorySectionViewModel
{
    public StoreCategoryViewModel Category { get; set; } = new();
    public List<StoreProductViewModel> Products { get; set; } = [];
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
    public decimal? SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string BrandImageUrl { get; set; } = string.Empty;
    public bool IsPreOrderEnabled { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechnicalSpecs { get; set; } = string.Empty;
    public string UsageGuide { get; set; } = string.Empty;
    public List<ProductUnitOption> UnitOptions { get; set; } = [];
    public List<string> GalleryImages { get; set; } = [];
    public string CategorySlug { get; set; } = "all";
    public string CategoryName { get; set; } = string.Empty;
    public List<StoreVariantViewModel> Variants { get; set; } = [];
}

public class StoreVariantViewModel
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int StockQuantity { get; set; }
}

public class ProductListPageViewModel
{
    public List<StoreCategoryViewModel> Categories { get; set; } = [];
    public List<StoreProductViewModel> Products { get; set; } = [];
    public List<string> Brands { get; set; } = [];
    public string Query { get; set; } = string.Empty;
    public string Category { get; set; } = "all";
    public string Brand { get; set; } = "all";
    public bool InStockOnly { get; set; }
    public string Sort { get; set; } = "newest";
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string FilterLabel { get; set; } = string.Empty;
}

public class ProductDetailPageViewModel
{
    public StoreProductViewModel Product { get; set; } = new();
    public List<StoreProductViewModel> RelatedProducts { get; set; } = [];
}

public class CartPageViewModel
{
    public List<StoreProductViewModel> SuggestedProducts { get; set; } = [];
}
