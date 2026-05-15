using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diagnova.Models;

public class VitalDefinition
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<VitalReadingEntry> Readings { get; set; } = new();
}

public class VitalReadingEntry
{
    public double Value { get; set; }
    public string? Note { get; set; }
    public DateTime RecordedAt { get; set; }
}