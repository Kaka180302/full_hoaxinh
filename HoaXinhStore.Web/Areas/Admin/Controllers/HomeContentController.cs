using HoaXinhStore.Web.Services.HomeContent;
using HoaXinhStore.Web.Services.Policies;
using HoaXinhStore.Web.Hubs;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class HomeContentController(
    IHomeContentService homeContentService,
    IPolicyContentService policyContentService,
    IWebHostEnvironment env,
    IHubContext<StorefrontHub> hub) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await homeContentService.GetAsync();
        var policies = await policyContentService.GetAllAsync();

        var vm = BuildViewModel(settings, policies);
        vm.Message = TempData["HomeContentMessage"] as string;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        HomeContentEditViewModel vm,
        List<string>? ExistingBannerImages,
        List<IFormFile>? BannerFiles)
    {
        vm.FeaturedLimit = vm.FeaturedLimit <= 0 ? 8 : vm.FeaturedLimit;
        vm.CategorySectionProductLimit = vm.CategorySectionProductLimit <= 0 ? 5 : vm.CategorySectionProductLimit;

        // HomeContent form has many dynamic fields (JS add/remove/reindex),
        // so we normalize and continue saving instead of blocking on incidental model-state noise.

        var mergedBanners = new List<string>();
        if (ExistingBannerImages is not null)
        {
            mergedBanners.AddRange(ExistingBannerImages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }

        if (BannerFiles is not null && BannerFiles.Count > 0)
        {
            var uploaded = await SaveBannerFilesAsync(BannerFiles);
            mergedBanners.AddRange(uploaded);
        }

        var sectionMap = (vm.Sections ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key.Trim().ToLowerInvariant(), x => x);

        HomeSectionSetting ToSection(string key, int fallbackOrder, bool fallbackVisible, bool fallbackSlider)
        {
            if (!sectionMap.TryGetValue(key, out var src))
            {
                return HomeSectionSetting.CreateDefault(key, fallbackOrder, fallbackVisible, fallbackSlider, false);
            }

            return new HomeSectionSetting
            {
                Key = key,
                SortOrder = src.SortOrder,
                IsVisible = src.IsVisible,
                IsSlider = src.IsSlider,
                AutoSlide = src.IsSlider && src.AutoSlide
            };
        }

        var footerColumns = (vm.FooterColumns ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .Select(c => new HomeFooterColumn
            {
                Title = c.Title.Trim(),
                Items = (c.Items ?? [])
                    .Where(i =>
                    {
                        var type = NormalizeFooterItemType(i.ItemType);
                        return type == "policy"
                            ? !string.IsNullOrWhiteSpace(i.PolicyKey)
                            : !string.IsNullOrWhiteSpace(i.Label);
                    })
                    .Select(i => new HomeFooterColumnItem
                    {
                        Label = (i.Label ?? string.Empty).Trim(),
                        Url = (i.Url ?? string.Empty).Trim(),
                        ItemType = NormalizeFooterItemType(i.ItemType),
                        PolicyKey = (i.PolicyKey ?? string.Empty).Trim()
                    })
                    .ToList()
            })
            .ToList();

        var policyData = await policyContentService.GetAllAsync();
        EnsurePolicyColumnSynced(footerColumns, policyData);

        var settings = new HomeContentSettings
        {
            FeaturedLimit = vm.FeaturedLimit,
            CategorySectionProductLimit = vm.CategorySectionProductLimit,
            BannerImages = mergedBanners,
            FeaturedSection = ToSection("featured", 1, true, true),
            CategorySection = ToSection("category", 2, true, false),
            BrandSection = ToSection("brand", 3, true, false),
            FooterColumns = footerColumns
        };

        await homeContentService.SaveAsync(settings);
        await hub.Clients.All.SendAsync("storefront-updated", new { type = "home-content", at = DateTimeOffset.UtcNow });

        TempData["HomeContentMessage"] = "Đã lưu cấu hình trang chủ.";
        return RedirectToAction(nameof(Index));
    }

    private HomeContentEditViewModel BuildViewModel(HomeContentSettings settings, Dictionary<string, PolicyContentItem> policies)
    {
        var vm = new HomeContentEditViewModel
        {
            FeaturedLimit = settings.FeaturedLimit,
            CategorySectionProductLimit = settings.CategorySectionProductLimit,
            BannerImages = settings.BannerImages.ToList(),
            Sections =
            [
                new()
                {
                    Key = "featured",
                    Label = "Sản phẩm nổi bật",
                    SortOrder = settings.FeaturedSection.SortOrder,
                    IsVisible = settings.FeaturedSection.IsVisible,
                    IsSlider = settings.FeaturedSection.IsSlider,
                    AutoSlide = settings.FeaturedSection.AutoSlide
                },
                new()
                {
                    Key = "category",
                    Label = "Danh mục sản phẩm",
                    SortOrder = settings.CategorySection.SortOrder,
                    IsVisible = settings.CategorySection.IsVisible,
                    IsSlider = settings.CategorySection.IsSlider,
                    AutoSlide = settings.CategorySection.AutoSlide
                },
                new()
                {
                    Key = "brand",
                    Label = "Thương hiệu",
                    SortOrder = settings.BrandSection.SortOrder,
                    IsVisible = settings.BrandSection.IsVisible,
                    IsSlider = settings.BrandSection.IsSlider,
                    AutoSlide = settings.BrandSection.AutoSlide
                }
            ],
            FooterColumns = settings.FooterColumns
                .Select(c => new HomeFooterColumnInput
                {
                    Title = c.Title,
                    Items = c.Items.Select(i => new HomeFooterColumnItemInput
                    {
                        Label = i.Label,
                        Url = i.Url,
                        ItemType = NormalizeFooterItemType(i.ItemType),
                        PolicyKey = i.PolicyKey
                    }).ToList()
                }).ToList(),
            PolicyItems = policies.Select(x => new PolicyEditViewModel
            {
                Key = x.Key,
                Title = x.Value.Title,
                Source = x.Value.Source,
                Content = x.Value.Content
            }).ToList()
        };

        return EnsureViewModelShape(vm, settings, policies);
    }

    private static HomeContentEditViewModel EnsureViewModelShape(
        HomeContentEditViewModel vm,
        HomeContentSettings settings,
        Dictionary<string, PolicyContentItem> policies)
    {
        vm.BannerImages ??= settings.BannerImages.ToList();
        vm.Sections ??= [];
        vm.FooterColumns ??= [];
        vm.PolicyItems ??= [];

        if (vm.Sections.Count == 0)
        {
            vm.Sections =
            [
                new() { Key = "featured", Label = "Sản phẩm nổi bật", SortOrder = settings.FeaturedSection.SortOrder, IsVisible = settings.FeaturedSection.IsVisible, IsSlider = settings.FeaturedSection.IsSlider, AutoSlide = settings.FeaturedSection.AutoSlide },
                new() { Key = "category", Label = "Danh mục sản phẩm", SortOrder = settings.CategorySection.SortOrder, IsVisible = settings.CategorySection.IsVisible, IsSlider = settings.CategorySection.IsSlider, AutoSlide = settings.CategorySection.AutoSlide },
                new() { Key = "brand", Label = "Thương hiệu", SortOrder = settings.BrandSection.SortOrder, IsVisible = settings.BrandSection.IsVisible, IsSlider = settings.BrandSection.IsSlider, AutoSlide = settings.BrandSection.AutoSlide }
            ];
        }

        foreach (var col in vm.FooterColumns)
        {
            col.Items ??= [];
        }

        if (vm.PolicyItems.Count == 0)
        {
            vm.PolicyItems = policies.Select(x => new PolicyEditViewModel
            {
                Key = x.Key,
                Title = x.Value.Title,
                Source = x.Value.Source,
                Content = x.Value.Content
            }).ToList();
        }

        return vm;
    }

    private async Task<List<string>> SaveBannerFilesAsync(IEnumerable<IFormFile> files)
    {
        var uploaded = new List<string>();
        var folder = Path.Combine(env.WebRootPath, "uploads", "home", "banners");
        Directory.CreateDirectory(folder);

        foreach (var file in files.Where(f => f is { Length: > 0 }))
        {
            var ext = Path.GetExtension(file.FileName);
            var safeExt = string.IsNullOrWhiteSpace(ext) ? ".jpg" : ext.ToLowerInvariant();
            var fileName = $"home_banner_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{safeExt}";
            var path = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(path);
            await file.CopyToAsync(stream);
            uploaded.Add($"/uploads/home/banners/{fileName}");
        }

        return uploaded;
    }

    private static string NormalizeFooterItemType(string? value)
    {
        var type = (value ?? string.Empty).Trim().ToLowerInvariant();
        return type switch
        {
            "link" => "link",
            "policy" => "policy",
            _ => "text"
        };
    }

    private static void EnsurePolicyColumnSynced(
        List<HomeFooterColumn> footerColumns,
        Dictionary<string, PolicyContentItem> policyData)
    {
        footerColumns ??= [];
        policyData ??= new Dictionary<string, PolicyContentItem>(StringComparer.OrdinalIgnoreCase);

        var policyColumn = footerColumns.FirstOrDefault(c =>
            string.Equals((c.Title ?? string.Empty).Trim(), "Chính sách", StringComparison.OrdinalIgnoreCase));

        if (policyColumn is null)
        {
            policyColumn = new HomeFooterColumn
            {
                Title = "Chính sách",
                Items = []
            };
            footerColumns.Add(policyColumn);
        }

        var existingByKey = (policyColumn.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.PolicyKey))
            .GroupBy(i => i.PolicyKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var synced = new List<HomeFooterColumnItem>();
        foreach (var pair in policyData.OrderBy(x => x.Value?.Title ?? x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            existingByKey.TryGetValue(key, out var existing);
            synced.Add(new HomeFooterColumnItem
            {
                ItemType = "policy",
                PolicyKey = key,
                Url = string.Empty,
                Label = !string.IsNullOrWhiteSpace(existing?.Label)
                    ? existing.Label.Trim()
                    : (pair.Value?.Title ?? key).Trim()
            });
        }

        policyColumn.Items = synced;
    }
}
