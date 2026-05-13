namespace Diagnova.Models;

/// <summary>OpenAI JSON classification: symptom + profile risk.</summary>
public sealed class AlarmingClassification
{
    public bool IsAlarming { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SpecialistHint { get; set; } = string.Empty;
}

/// <summary>Facility row returned to the browser (distances computed server-side).</summary>
public sealed class EmergencyFacilityClientDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
    public string? Notes { get; set; }
}
