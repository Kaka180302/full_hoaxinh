namespace HoaXinhStore.Web.Services.HomeContent;

public class HomeContentSettings
{
    public HomeSectionSetting FeaturedSection { get; set; } = HomeSectionSetting.CreateDefault("featured", 1, true, true, false);
    public HomeSectionSetting CategorySection { get; set; } = HomeSectionSetting.CreateDefault("category", 2, true, false, false);
    public HomeSectionSetting BrandSection { get; set; } = HomeSectionSetting.CreateDefault("brand", 3, true, false, false);

    public int FeaturedLimit { get; set; } = 8;
    public int CategorySectionProductLimit { get; set; } = 5;

    public List<string> BannerImages { get; set; } =
    [
        "/assets/img/banner/banner.jpg",
        "/assets/img/banner/banner.jpg",
        "/assets/img/banner/banner.jpg",
        "/assets/img/banner/banner.jpg"
    ];

    public List<HomeFooterColumn> FooterColumns { get; set; } =
    [
        new()
        {
            Title = "Liên hệ",
            Items =
            [
                new() { Label = "68 Nguyễn Huệ, Phường Sài Gòn, TP HCM" },
                new() { Label = "08.1998.1900", Url = "tel:0819981900" },
                new() { Label = "infor@hoaxinhgroup.vn", Url = "mailto:infor@hoaxinhgroup.vn" }
            ]
        },
        new()
        {
            Title = "Giờ làm việc",
            Items =
            [
                new() { Label = "8:00 - 19:00" },
                new() { Label = "Thứ 2 - Thứ 7" }
            ]
        },
        new()
        {
            Title = "Trang website",
            Items =
            [
                new() { Label = "Sản phẩm", Url = "/Store/Products" },
                new() { Label = "Nổi bật", Url = "/Store#featured" },
                new() { Label = "Tra cứu đơn hàng", Url = "/Store/TrackOrder" }
            ]
        },
        new()
        {
            Title = "Chính sách",
            Items =
            [
                new() { Label = "Về Chúng Tôi", Url = "/Store/Policy?key=about" },
                new() { Label = "Hướng dẫn mua hàng", Url = "/Store/Policy?key=buy-guide" },
                new() { Label = "Chính sách thanh toán", Url = "/Store/Policy?key=payment" }
            ]
        }
    ];

    public List<HomeFooterLinkItem> FooterLinks { get; set; } = [];

    public bool ShowFeaturedSection
    {
        get => FeaturedSection.IsVisible;
        set => FeaturedSection.IsVisible = value;
    }

    public bool ShowBrandSection
    {
        get => BrandSection.IsVisible;
        set => BrandSection.IsVisible = value;
    }
}

public class HomeFooterLinkItem
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class HomeFooterColumn
{
    public string Title { get; set; } = string.Empty;
    public List<HomeFooterColumnItem> Items { get; set; } = [];
}

public class HomeFooterColumnItem
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ItemType { get; set; } = "text"; // text | link | policy
    public string PolicyKey { get; set; } = string.Empty;
}

public class HomeSectionSetting
{
    public string Key { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 1;
    public bool IsVisible { get; set; } = true;
    public bool IsSlider { get; set; } = false;
    public bool AutoSlide { get; set; } = false;

    public static HomeSectionSetting CreateDefault(string key, int sortOrder, bool isVisible, bool isSlider, bool autoSlide)
    {
        return new HomeSectionSetting
        {
            Key = key,
            SortOrder = sortOrder,
            IsVisible = isVisible,
            IsSlider = isSlider,
            AutoSlide = autoSlide
        };
    }
}
