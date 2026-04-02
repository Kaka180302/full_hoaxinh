namespace HoaXinhStore.Web.Services.Policies;

public interface IPolicyContentService
{
    Task<Dictionary<string, PolicyContentItem>> GetAllAsync();
    Task SaveAllAsync(Dictionary<string, PolicyContentItem> data);
}

