namespace HoaXinhStore.Web.Services.HomeContent;

public interface IHomeContentService
{
    Task<HomeContentSettings> GetAsync();
    Task SaveAsync(HomeContentSettings settings);
}

