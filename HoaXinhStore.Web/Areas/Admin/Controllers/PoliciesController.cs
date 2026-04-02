using HoaXinhStore.Web.Services.Policies;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.RegularExpressions;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PoliciesController(IPolicyContentService policyService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var data = await policyService.GetAllAsync();
        var vm = new PolicyManagementViewModel
        {
            Message = TempData["PolicyMessage"] as string,
            Items = data.Select(x => new PolicyEditViewModel
            {
                Key = x.Key,
                Title = x.Value.Title,
                Source = x.Value.Source,
                Content = ToEditorPlainText(x.Value.Content)
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PolicyManagementViewModel vm)
    {
        var data = vm.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key.Trim(),
                x => new PolicyContentItem
                {
                    Title = x.Title?.Trim() ?? string.Empty,
                    Source = x.Source?.Trim() ?? string.Empty,
                    Content = (x.Content ?? string.Empty).Trim()
                });

        await policyService.SaveAllAsync(data);
        TempData["PolicyMessage"] = "Đã lưu nội dung chính sách.";
        return RedirectToAction(nameof(Index));
    }

    private static string ToEditorPlainText(string htmlOrText)
    {
        if (string.IsNullOrWhiteSpace(htmlOrText))
        {
            return string.Empty;
        }

        var text = htmlOrText
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase);

        text = Regex.Replace(text, "<.*?>", string.Empty, RegexOptions.Singleline);
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r\n", "\n");
        text = Regex.Replace(text, "\n{3,}", "\n\n");

        return text.Trim();
    }
}
