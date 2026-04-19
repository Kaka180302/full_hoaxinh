using HoaXinhStore.Web.Services.Shipping;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ShippingSettingsController(IShippingSettingsService shippingSettingsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await shippingSettingsService.GetAsync();
        var vm = ToVm(settings);
        vm.WebhookUrl = BuildWebhookUrl();
        vm.Message = TempData["ShippingSettingsMessage"] as string ?? string.Empty;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ShippingSettingsViewModel vm)
    {
        var settings = new ShippingSettings
        {
            GhnEnabled = vm.GhnEnabled,
            ProviderName = "GHN",
            WebhookKey = (vm.WebhookKey ?? string.Empty).Trim(),
            DefaultCarrierDisplayName = string.IsNullOrWhiteSpace(vm.DefaultCarrierDisplayName)
                ? "GHN"
                : vm.DefaultCarrierDisplayName.Trim(),
            AutoUpdateOrderStatus = vm.AutoUpdateOrderStatus,
            AutoMarkCodPaidWhenDelivered = vm.AutoMarkCodPaidWhenDelivered,
            Note = (vm.Note ?? string.Empty).Trim()
        };
        await shippingSettingsService.SaveAsync(settings);
        TempData["ShippingSettingsMessage"] = "Đã lưu cấu hình GHN tracking.";
        return RedirectToAction(nameof(Index));
    }

    private ShippingSettingsViewModel ToVm(ShippingSettings settings)
    {
        return new ShippingSettingsViewModel
        {
            GhnEnabled = settings.GhnEnabled,
            ProviderName = "GHN",
            WebhookKey = settings.WebhookKey,
            DefaultCarrierDisplayName = settings.DefaultCarrierDisplayName,
            AutoUpdateOrderStatus = settings.AutoUpdateOrderStatus,
            AutoMarkCodPaidWhenDelivered = settings.AutoMarkCodPaidWhenDelivered,
            Note = settings.Note
        };
    }

    private string BuildWebhookUrl()
    {
        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        return $"{scheme}://{host}/api/shipping/webhook";
    }
}

