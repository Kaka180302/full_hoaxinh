using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HoaXinhStore.Web.Services;

public static class ProductContentMeta
{
    private const string MarkerStart = "<!--HX_META:";
    private const string MarkerEnd = "-->";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ParsedProductContent Parse(string? rawDescription)
    {
        var raw = rawDescription ?? string.Empty;
        var start = raw.IndexOf(MarkerStart, StringComparison.Ordinal);
        if (start < 0)
        {
            return new ParsedProductContent { CleanDescription = raw.Trim() };
        }

        var end = raw.IndexOf(MarkerEnd, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return new ParsedProductContent { CleanDescription = raw.Trim() };
        }

        var jsonStart = start + MarkerStart.Length;
        var json = raw.Substring(jsonStart, end - jsonStart).Trim();
        var clean = (raw.Substring(0, start) + raw[(end + MarkerEnd.Length)..]).Trim();

        try
        {
            var meta = JsonSerializer.Deserialize<ProductMetaPayload>(json, JsonOptions) ?? new ProductMetaPayload();
            meta.UnitOptions = (meta.UnitOptions ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Factor > 0)
                .Select(x => new ProductUnitOption
                {
                    Name = x.Name.Trim(),
                    Factor = x.Factor
                })
                .ToList();

            return new ParsedProductContent
            {
                CleanDescription = clean,
                TechnicalSpecs = meta.TechnicalSpecs ?? string.Empty,
                UsageGuide = meta.UsageGuide ?? string.Empty,
                UnitOptions = meta.UnitOptions,
                GalleryImages = (meta.GalleryImages ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()
            };
        }
        catch
        {
            return new ParsedProductContent { CleanDescription = raw.Trim() };
        }
    }

    public static string Compose(
        string? description,
        string? technicalSpecs,
        string? usageGuide,
        IEnumerable<ProductUnitOption>? unitOptions,
        IEnumerable<string>? galleryImages)
    {
        var baseContent = RemoveMetaBlock(description ?? string.Empty).Trim();
        var normalizedUnits = (unitOptions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Factor > 0)
            .Select(x => new ProductUnitOption
            {
                Name = x.Name.Trim(),
                Factor = x.Factor
            })
            .ToList();

        var hasMeta = !string.IsNullOrWhiteSpace(technicalSpecs)
                      || !string.IsNullOrWhiteSpace(usageGuide)
                      || normalizedUnits.Count > 0
                      || (galleryImages?.Any() ?? false);

        if (!hasMeta)
        {
            return baseContent;
        }

        var payload = new ProductMetaPayload
        {
            TechnicalSpecs = technicalSpecs?.Trim(),
            UsageGuide = usageGuide?.Trim(),
            UnitOptions = normalizedUnits,
            GalleryImages = (galleryImages ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return $"{baseContent}\n\n{MarkerStart}{json}{MarkerEnd}";
    }

    private static string RemoveMetaBlock(string input)
    {
        return Regex.Replace(
            input,
            @"<!--HX_META:.*?-->",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();
    }

    private sealed class ProductMetaPayload
    {
        public string? TechnicalSpecs { get; set; }
        public string? UsageGuide { get; set; }
        public List<ProductUnitOption> UnitOptions { get; set; } = [];
        public List<string> GalleryImages { get; set; } = [];
    }
}

public class ParsedProductContent
{
    public string CleanDescription { get; set; } = string.Empty;
    public string TechnicalSpecs { get; set; } = string.Empty;
    public string UsageGuide { get; set; } = string.Empty;
    public List<ProductUnitOption> UnitOptions { get; set; } = [];
    public List<string> GalleryImages { get; set; } = [];
}

public class ProductUnitOption
{
    public string Name { get; set; } = string.Empty;
    public int Factor { get; set; } = 1;
}
