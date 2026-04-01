using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Controllers.Api;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProductListResponse>> GetProducts([FromQuery] int page = 0, [FromQuery] int size = 100)
    {
        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Id)
            .Skip(page * size)
            .Take(size)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                ImageURL = p.ImageUrl,
                Summary = p.Summary,
                Descriptions = p.Descriptions,
                CategoryId = new CategoryDto
                {
                    Id = p.Category!.Id,
                    Name = p.Category.Name
                }
            })
            .ToListAsync();

        return Ok(new ProductListResponse { Content = products });
    }
}
