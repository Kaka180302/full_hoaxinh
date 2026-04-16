using HoaXinhStore.Web.Entities;

namespace HoaXinhStore.Web.Services.Inventory;

public interface IInventoryService
{
    Task<bool> TryReserveVariantAsync(int variantId, int quantity);
    Task ReleaseVariantReservationsAsync(IEnumerable<(int VariantId, int Quantity)> reservations);
    Task ReleaseOrderReservationsAsync(Order order);
    Task ConsumeOrderReservationsAsync(Order order);
}
