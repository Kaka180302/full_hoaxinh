namespace HoaXinhStore.Web.ViewModels.Admin;

public class HomeContentEditViewModel
{
    public int FeaturedLimit { get; set; } = 8;

    public int CategorySectionProductLimit { get; set; } = 5;

    public List<string> BannerImages { get; set; } = [];
    public List<HomeSectionEditInput> Sections { get; set; } = [];
    public List<HomeFooterColumnInput> FooterColumns { get; set; } = [];
    public List<PolicyEditViewModel> PolicyItems { get; set; } = [];
    public string? Message { get; set; }
}

public class HomeSectionEditInput
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 1;
    public bool IsVisible { get; set; } = true;
    public bool IsSlider { get; set; } = false;
    public bool AutoSlide { get; set; } = false;
}

public class HomeFooterColumnInput
{
    public string Title { get; set; } = string.Empty;
    public List<HomeFooterColumnItemInput> Items { get; set; } = [];
}

public class HomeFooterColumnItemInput
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ItemType { get; set; } = "text";
    public string PolicyKey { get; set; } = string.Empty;
}
