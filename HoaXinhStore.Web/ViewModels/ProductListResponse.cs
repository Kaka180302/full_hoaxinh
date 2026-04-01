namespace HoaXinhStore.Web.ViewModels;

public class ProductListResponse
{
    public List<ProductDto> Content { get; set; } = [];
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageURL { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Descriptions { get; set; } = string.Empty;
    public CategoryDto CategoryId { get; set; } = new();
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
