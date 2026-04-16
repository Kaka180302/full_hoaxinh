using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Services;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (await db.Categories.AnyAsync() || await db.Products.AnyAsync())
        {
            return;
        }

        var myPham = new Category { Name = "mypham" };
        var thucPham = new Category { Name = "thucpham" };
        var thietBi = new Category { Name = "thietbi" };

        db.Categories.AddRange(myPham, thucPham, thietBi);
        await db.SaveChangesAsync();

        db.Products.AddRange(
            new Product
            {
                Sku = "MP-0001",
                Name = "Kem Duong Am KICHO",
                Price = 420000,
                StockQuantity = 50,
                ImageUrl = "/assets/img/kem-duong-da/mp_kemduongam.png",
                Summary = "Duong am va bao ve da",
                Descriptions = "Kem duong am danh cho da kho",
                CategoryId = myPham.Id
            },
            new Product
            {
                Sku = "TB-0001",
                Name = "Xit phong Shay",
                Price = 99000,
                StockQuantity = 80,
                ImageUrl = "/assets/img/gia-dung/tbgd_xitphong.jpg",
                Summary = "Mui huong nhe, khang mui",
                Descriptions = "Su dung cho phong khach va phong ngu",
                CategoryId = thietBi.Id
            },
            new Product
            {
                Sku = "TP-0001",
                Name = "Thach nghe Nano365",
                Price = 250000,
                StockQuantity = 60,
                ImageUrl = "/assets/img/thuc-pham/tpcn/tp_thachnghe.jpg",
                Summary = "Bo sung dinh duong",
                Descriptions = "San pham thuc pham bo sung",
                CategoryId = thucPham.Id
            }
        );

        await db.SaveChangesAsync();
    }
}
