namespace HoaXinhStore.Web.ViewModels.Admin;

public class PolicyEditViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class PolicyManagementViewModel
{
    public List<PolicyEditViewModel> Items { get; set; } = [];
    public string? Message { get; set; }
}

