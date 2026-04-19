using HoaXinhStore.Web.Services.Payments;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PaymentMethodsController(IPaymentMethodSettingsService paymentMethodSettingsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await paymentMethodSettingsService.GetAsync();
        return View(new PaymentMethodSettingsViewModel
        {
            CodEnabled = settings.CodEnabled,
            VnPayEnabled = settings.VnPayEnabled,
            QrPayEnabled = settings.QrPayEnabled,
            AutoQrEnabled = settings.AutoQrEnabled,
            Message = TempData["PaymentMethodSettingsMessage"] as string ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PaymentMethodSettingsViewModel vm)
    {
        var settings = new PaymentMethodSettings
        {
            CodEnabled = vm.CodEnabled,
            VnPayEnabled = vm.VnPayEnabled,
            QrPayEnabled = vm.QrPayEnabled,
            AutoQrEnabled = vm.AutoQrEnabled
        };
        await paymentMethodSettingsService.SaveAsync(settings);
        TempData["PaymentMethodSettingsMessage"] = "Đã lưu cấu hình phương thức thanh toán.";
        return RedirectToAction(nameof(Index));
    }
}

