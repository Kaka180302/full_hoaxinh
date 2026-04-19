using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities;
using HoaXinhStore.Web.Options;
using HoaXinhStore.Web.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HoaXinhStore.Web.Services.Orders;

public class OrderPaymentTimeoutWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderPaymentTimeoutOptions> options,
    ILogger<OrderPaymentTimeoutWorker> logger) : BackgroundService
{
    private readonly OrderPaymentTimeoutOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(_options.SweepIntervalSeconds, 30, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled)
                {
                    await SweepExpiredOrdersAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Order payment-timeout sweep failed.");
            }

            try
            {
                var hasNext = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNext) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SweepExpiredOrdersAsync(CancellationToken ct)
    {
        var timeoutMinutes = Math.Clamp(_options.TimeoutMinutes, 1, 24 * 60);
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-timeoutMinutes);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        var expiredOrders = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o =>
                (o.PaymentStatus == "AwaitingGateway" || o.PaymentStatus == "AwaitingCustomerTransfer")
                && o.CreatedAtUtc <= cutoffUtc)
            .ToListAsync(ct);

        if (expiredOrders.Count == 0) return;

        foreach (var order in expiredOrders)
        {
            var latestPayment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
            if (latestPayment is not null && string.Equals(latestPayment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fromStatus = order.OrderStatus;
            await inventoryService.ReleaseOrderReservationsAsync(order);

            order.OrderStatus = "Cancelled";
            order.PaymentStatus = "Cancelled";

            if (latestPayment is not null && !string.Equals(latestPayment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                latestPayment.Status = "Cancelled";
            }

            db.OrderTimelines.Add(new OrderTimeline
            {
                OrderId = order.Id,
                Action = "PaymentTimeoutCancelled",
                FromStatus = fromStatus,
                ToStatus = order.OrderStatus,
                Note = $"Đơn tự hủy do quá hạn thanh toán ({timeoutMinutes} phút)."
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Payment-timeout worker cancelled {Count} expired order(s).", expiredOrders.Count);
    }
}
