namespace Diagnova.Services;

public sealed class EmergencyDetectorService : IEmergencyDetectorService
{
    private static readonly string[] Keywords =
    [
        "chest pain", "can't breathe", "cannot breathe", "cant breathe", "difficulty breathing",
        "shortness of breath", "stroke", "heavy bleeding", "unconscious", "suicide",
        "severe allergic", "anaphylaxis", "heart attack", "cardiac arrest",
        "slurred speech", "sudden weakness", "worst headache",
    ];

    public EmergencyScanResult Scan(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new EmergencyScanResult(false, Array.Empty<string>());

        var normalized = text.Trim().ToLowerInvariant();
        var matched = new List<string>();

        foreach (var kw in Keywords)
        {
            if (normalized.Contains(kw, StringComparison.Ordinal))
                matched.Add(kw);
        }

        return new EmergencyScanResult(matched.Count > 0, matched.Distinct(StringComparer.Ordinal).ToList());
    }
}
