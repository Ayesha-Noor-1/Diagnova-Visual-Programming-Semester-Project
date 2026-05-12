namespace Diagnova.Services;

public interface IEmergencyDetectorService
{
    EmergencyScanResult Scan(string text);
}

public sealed record EmergencyScanResult(bool IsEmergency, IReadOnlyList<string> MatchedKeywords);
