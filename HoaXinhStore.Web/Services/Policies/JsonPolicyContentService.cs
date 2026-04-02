using System.Text.Json;

namespace HoaXinhStore.Web.Services.Policies;

public class JsonPolicyContentService(IWebHostEnvironment env) : IPolicyContentService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private string FilePath => Path.Combine(env.ContentRootPath, "Data", "policies.json");

    public async Task<Dictionary<string, PolicyContentItem>> GetAllAsync()
    {
        EnsureFileExists();
        await using var stream = File.OpenRead(FilePath);
        var data = await JsonSerializer.DeserializeAsync<Dictionary<string, PolicyContentItem>>(stream, _jsonOptions);
        return data ?? new Dictionary<string, PolicyContentItem>();
    }

    public async Task SaveAllAsync(Dictionary<string, PolicyContentItem> data)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions);
    }

    private void EnsureFileExists()
    {
        if (File.Exists(FilePath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(FilePath, "{}");
    }
}
