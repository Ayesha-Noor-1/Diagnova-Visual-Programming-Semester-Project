using System.Text.Json;

namespace Diagnova.Services;

public sealed class OpenFdaService : IOpenFdaService
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenFdaService> _logger;

    public OpenFdaService(HttpClient http, ILogger<OpenFdaService> logger)
    {
        _http = http;
        _logger = logger;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.fda.gov/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<OpenFdaDrugLabel?> SearchDrugLabelAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var term = query.Trim().Replace("\"", "", StringComparison.Ordinal);
        var escaped = term.Replace("\\", "\\\\", StringComparison.Ordinal);

        var searchQueries = new[]
        {
            $"(openfda.brand_name:\"{escaped}\" OR openfda.generic_name:\"{escaped}\")",
            $"(openfda.brand_name:{escaped} OR openfda.generic_name:{escaped})",
            $"(openfda.substance_name:\"{escaped}\" OR openfda.substance_name:{escaped})",
            $"openfda.brand_name:\"*{escaped}*\"",
            $"active_ingredient:\"{escaped}\"",
            $"_all:\"{escaped}\"",
        };

        foreach (var search in searchQueries)
        {
            var result = await TrySearchAsync(search, cancellationToken);
            if (result != null)
                return result;
        }

        return null;
    }

    private async Task<OpenFdaDrugLabel?> TrySearchAsync(string search, CancellationToken cancellationToken)
    {
        var uri = $"drug/label.json?search={Uri.EscapeDataString(search)}&limit=1";

        try
        {
            using var response = await _http.GetAsync(uri, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("OpenFDA query {Search} returned {Status}", search, response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            return ParseLabel(results[0]);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OpenFDA query failed: {Search}", search);
            return null;
        }
    }

    private static OpenFdaDrugLabel? ParseLabel(JsonElement first)
    {
        var openfda = first.TryGetProperty("openfda", out var of) ? of : default;

        string? Brand(JsonElement e) =>
            e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0 ? e[0].GetString() : null;

        return new OpenFdaDrugLabel
        {
            BrandName = openfda.ValueKind == JsonValueKind.Object && openfda.TryGetProperty("brand_name", out var bn)
                ? Brand(bn)
                : null,
            GenericName = openfda.ValueKind == JsonValueKind.Object && openfda.TryGetProperty("generic_name", out var gn)
                ? Brand(gn)
                : null,
            Purpose = first.TryGetProperty("purpose", out var p) ? JoinLabelArray(p) : null,
            DosageAndAdministration = first.TryGetProperty("dosage_and_administration", out var d) ? JoinLabelArray(d) : null,
            Warnings = first.TryGetProperty("warnings", out var w) ? JoinLabelArray(w) : null,
            AdverseReactions = first.TryGetProperty("adverse_reactions", out var ar) ? JoinLabelArray(ar) : null,
            Contraindications = first.TryGetProperty("contraindications", out var co) ? JoinLabelArray(co) : null,
        };
    }

    private static string? JoinLabelArray(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() == 0)
            return null;

        var parts = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                parts.Add(item.GetString() ?? string.Empty);
        }

        var text = string.Join("\n\n", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
