using System.Text.Json;
using HoaXinhStore.Web.Options;
using Microsoft.Extensions.Options;

namespace HoaXinhStore.Web.Services.Shipping;

public class JsonShippingSettingsService(
    IWebHostEnvironment env,
    IOptions<ShippingIntegrationOptions> fallbackOptions) : IShippingSettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string FilePath => Path.Combine(env.ContentRootPath, "Data", "shipping-settings.json");

    public async Task<ShippingSettings> GetAsync()
    {
        EnsureFileExists();
        await using var stream = File.OpenRead(FilePath);
        var data = await JsonSerializer.DeserializeAsync<ShippingSettings>(stream, _jsonOptions);
        return Normalize(data ?? new ShippingSettings(), fallbackOptions.Value);
    }

    public async Task SaveAsync(ShippingSettings settings)
    {
        var normalized = Normalize(settings, fallbackOptions.Value);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, normalized, _jsonOptions);
    }

    private void EnsureFileExists()
    {
        if (File.Exists(FilePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var seeded = Normalize(new ShippingSettings(), fallbackOptions.Value);
        var json = JsonSerializer.Serialize(seeded, _jsonOptions);
        File.WriteAllText(FilePath, json);
    }

    private static ShippingSettings Normalize(ShippingSettings settings, ShippingIntegrationOptions fallback)
    {
        settings.ProviderName = "GHN";
        settings.DefaultCarrierDisplayName = string.IsNullOrWhiteSpace(settings.DefaultCarrierDisplayName)
            ? "GHN"
            : settings.DefaultCarrierDisplayName.Trim();
        settings.WebhookKey = string.IsNullOrWhiteSpace(settings.WebhookKey)
            ? (fallback.WebhookKey ?? string.Empty).Trim()
            : settings.WebhookKey.Trim();
        settings.Note = (settings.Note ?? string.Empty).Trim();
        return settings;
    }
}

