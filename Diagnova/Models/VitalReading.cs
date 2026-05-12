namespace Diagnova.Models;

public class VitalReading
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? SystolicMmHg { get; set; }

    public int? DiastolicMmHg { get; set; }

    public double? BloodSugarMgDl { get; set; }

    public double? TemperatureC { get; set; }

    public string? Notes { get; set; }
}
