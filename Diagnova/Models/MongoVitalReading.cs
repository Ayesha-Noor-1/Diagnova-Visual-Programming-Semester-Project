using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Diagnova.Models;

public class MongoVitalReading
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? SystolicMmHg { get; set; }

    public int? DiastolicMmHg { get; set; }

    public double? BloodSugarMgDl { get; set; }

    public double? TemperatureC { get; set; }

    public string? Notes { get; set; }
}