using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoaXinhStore.Web.Services.Inventory;

public class InventoryService(AppDbContext db) : IInventoryService
{
    public async Task<bool> TryReserveVariantAsync(int variantId, int quantity)
    {
        var qty = Math.Max(1, quantity);
        var productId = await db.ProductVariants
            .Where(v => v.Id == variantId)
            .Select(v => (int?)v.ProductId)
            .FirstOrDefaultAsync();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [dbo].[ProductVariants]
SET [ReservedStock] = [ReservedStock] + {qty},
    [AvailableStock] = CASE
        WHEN [StockQuantity] - ([ReservedStock] + {qty}) < 0 THEN 0
        ELSE [StockQuantity] - ([ReservedStock] + {qty})
    END
WHERE [Id] = {variantId}
  AND [IsActive] = 1
  AND ([StockQuantity] - [ReservedStock]) >= {qty};");
        if (affected > 0 && productId.HasValue)
        {
            await SyncProductAvailableStocksAsync([productId.Value]);
        }
        return affected > 0;
    }

    public async Task ReleaseVariantReservationsAsync(IEnumerable<(int VariantId, int Quantity)> reservations)
    {
        var grouped = reservations
            .Where(x => x.VariantId > 0 && x.Quantity > 0)
            .GroupBy(x => x.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
        if (grouped.Count == 0) return;

        var variantIds = grouped.Select(x => x.VariantId).ToList();
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync();
        foreach (var row in grouped)
        {
            var variant = variants.FirstOrDefault(v => v.Id == row.VariantId);
            if (variant is null) continue;
            variant.ReservedStock = Math.Max(0, variant.ReservedStock - row.Quantity);
            variant.AvailableStock = Math.Max(0, variant.StockQuantity - variant.ReservedStock);
        }

        await db.SaveChangesAsync();
        await SyncProductAvailableStocksAsync(variants.Select(v => v.ProductId).Distinct().ToList());
    }

    public async Task ReleaseOrderReservationsAsync(Order order)
    {
        var items = order.Items
            .Where(i => i.VariantId.HasValue)
            .Select(i => (VariantId: i.VariantId!.Value, Quantity: Math.Max(1, i.Quantity)))
            .ToList();
        await ReleaseVariantReservationsAsync(items);
    }

    public async Task ConsumeOrderReservationsAsync(Order order)
    {
        var grouped = order.Items
            .Where(i => i.VariantId.HasValue)
            .GroupBy(i => i.VariantId!.Value)
            .Select(g => new
            {
                VariantId = g.Key,
                Quantity = g.Sum(i => Math.Max(1, i.Quantity))
            })
            .ToList();
        var variantIds = grouped.Select(x => x.VariantId).ToList();
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync();
        foreach (var row in grouped)
        {
            var variant = variants.FirstOrDefault(v => v.Id == row.VariantId);
            if (variant is null) continue;
            // Safety fallback: even if reservation was not recorded correctly, still deduct from stock.
            var consumed = Math.Min(row.Quantity, Math.Max(0, variant.ReservedStock));
            if (consumed <= 0)
            {
                consumed = Math.Min(row.Quantity, Math.Max(0, variant.StockQuantity));
            }
            variant.StockQuantity = Math.Max(0, variant.StockQuantity - consumed);
            variant.ReservedStock = Math.Max(0, variant.ReservedStock - consumed);
            variant.AvailableStock = Math.Max(0, variant.StockQuantity - variant.ReservedStock);
        }

        // Non-variant order items: deduct directly on product stock as fallback.
        var nonVariantGrouped = order.Items
            .Where(i => !i.VariantId.HasValue)
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(i => Math.Max(1, i.Quantity))
            })
            .ToList();
        if (nonVariantGrouped.Count > 0)
        {
            var productIds = nonVariantGrouped.Select(x => x.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
            foreach (var row in nonVariantGrouped)
            {
                var product = products.FirstOrDefault(p => p.Id == row.ProductId);
                if (product is null) continue;
                product.StockQuantity = Math.Max(0, product.StockQuantity - row.Quantity);
                product.IsPreOrderEnabled = product.StockQuantity <= 0;
            }
        }

        await db.SaveChangesAsync();
        await SyncProductAvailableStocksAsync(variants.Select(v => v.ProductId).Distinct().ToList());
    }

    private async Task SyncProductAvailableStocksAsync(IReadOnlyCollection<int> productIds)
    {
        if (productIds.Count == 0) return;

        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        foreach (var p in products)
        {
            var total = await db.ProductVariants
                .Where(v => v.ProductId == p.Id && v.IsActive)
                .SumAsync(v => (int?)v.AvailableStock) ?? 0;
            p.StockQuantity = Math.Max(0, total);
            p.IsPreOrderEnabled = p.StockQuantity <= 0;
        }
        await db.SaveChangesAsync();
    }
}
