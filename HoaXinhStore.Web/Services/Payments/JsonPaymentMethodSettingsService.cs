using System.Text.Json;

namespace HoaXinhStore.Web.Services.Payments;

public class JsonPaymentMethodSettingsService(IWebHostEnvironment env) : IPaymentMethodSettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string FilePath => Path.Combine(env.ContentRootPath, "Data", "payment-method-settings.json");

    public async Task<PaymentMethodSettings> GetAsync()
    {
        EnsureFileExists();
        await using var stream = File.OpenRead(FilePath);
        var data = await JsonSerializer.DeserializeAsync<PaymentMethodSettings>(stream, _jsonOptions);
        return Normalize(data ?? new PaymentMethodSettings());
    }

    public async Task SaveAsync(PaymentMethodSettings settings)
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
        var seeded = JsonSerializer.Serialize(new PaymentMethodSettings(), _jsonOptions);
        File.WriteAllText(FilePath, seeded);
    }

    private static PaymentMethodSettings Normalize(PaymentMethodSettings settings)
    {
        if (!settings.CodEnabled && !settings.VnPayEnabled && !settings.QrPayEnabled && !settings.AutoQrEnabled)
        {
            // Always keep at least one method enabled to avoid blocking checkout entirely.
            settings.CodEnabled = true;
        }

        return settings;
    }
}

