namespace Diagnova.Models;

public sealed class SosTriggerRequest
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Note { get; set; }
}

public sealed class SosTriggerResponse
{
    public bool Ok { get; set; }
    public bool EmailSent { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationMapsUrl { get; set; }
    public string? SmsUrl { get; set; }
    public string? CondensedProfile { get; set; }
    public IReadOnlyList<EmergencyFacilityClientDto> Facilities { get; set; } = Array.Empty<EmergencyFacilityClientDto>();
}
