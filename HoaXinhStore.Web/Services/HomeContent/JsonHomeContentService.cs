using System.Text.Json;

namespace HoaXinhStore.Web.Services.HomeContent;

public class JsonHomeContentService(IWebHostEnvironment env) : IHomeContentService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string FilePath => Path.Combine(env.ContentRootPath, "Data", "home-content.json");

    public async Task<HomeContentSettings> GetAsync()
    {
        EnsureFileExists();
        await using var stream = File.OpenRead(FilePath);
        var data = await JsonSerializer.DeserializeAsync<HomeContentSettings>(stream, _jsonOptions);
        return Normalize(data ?? new HomeContentSettings());
    }

    public async Task SaveAsync(HomeContentSettings settings)
    {
        var normalized = Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, normalized, _jsonOptions);
    }

    private void EnsureFileExists()
    {
        if (File.Exists(FilePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(new HomeContentSettings(), _jsonOptions);
        File.WriteAllText(FilePath, json);
    }

    private static HomeContentSettings Normalize(HomeContentSettings settings)
    {
        settings.FeaturedSection ??= HomeSectionSetting.CreateDefault("featured", 1, true, true, false);
        settings.CategorySection ??= HomeSectionSetting.CreateDefault("category", 2, true, false, false);
        settings.BrandSection ??= HomeSectionSetting.CreateDefault("brand", 3, true, false, false);

        settings.FeaturedSection.Key = "featured";
        settings.CategorySection.Key = "category";
        settings.BrandSection.Key = "brand";

        settings.FeaturedSection.SortOrder = Math.Clamp(settings.FeaturedSection.SortOrder, 1, 20);
        settings.CategorySection.SortOrder = Math.Clamp(settings.CategorySection.SortOrder, 1, 20);
        settings.BrandSection.SortOrder = Math.Clamp(settings.BrandSection.SortOrder, 1, 20);

        settings.FeaturedLimit = Math.Clamp(settings.FeaturedLimit, 1, 24);
        settings.CategorySectionProductLimit = Math.Clamp(settings.CategorySectionProductLimit, 1, 12);
        settings.BannerImages = (settings.BannerImages ?? [])
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(12)
            .ToList();
        if (settings.BannerImages.Count == 0)
        {
            settings.BannerImages.Add("/assets/img/banner/banner.jpg");
        }

        settings.FooterColumns = (settings.FooterColumns ?? [])
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
                    .Take(12)
                    .ToList()
            })
            .Take(8)
            .ToList();

        if (settings.FooterColumns.Count == 0 && (settings.FooterLinks?.Count ?? 0) > 0)
        {
            settings.FooterColumns.Add(new HomeFooterColumn
            {
                Title = "Liên kết thêm",
                Items = settings.FooterLinks
                    .Where(x => !string.IsNullOrWhiteSpace(x.Label))
                    .Select(x => new HomeFooterColumnItem
                    {
                        Label = x.Label.Trim(),
                        Url = (x.Url ?? string.Empty).Trim(),
                        ItemType = "link",
                        PolicyKey = string.Empty
                    })
                    .Take(12)
                    .ToList()
            });
        }

        settings.FooterLinks = (settings.FooterLinks ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Label) && !string.IsNullOrWhiteSpace(x.Url))
            .Select(x => new HomeFooterLinkItem
            {
                Label = x.Label.Trim(),
                Url = x.Url.Trim()
            })
            .Take(12)
            .ToList();
        return settings;
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
}
