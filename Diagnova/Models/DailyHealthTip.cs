using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diagnova.Models;

public class DailyHealthTip
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRequired]
    public string UserId { get; set; } = string.Empty;

    /// <summary>UTC calendar date (midnight) this tip belongs to.</summary>
    public DateTime TipDate { get; set; }

    public string Category { get; set; } = "lifestyle";

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public List<string> Conditions { get; set; } = new();

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DailyTipResponse
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string TipDate { get; set; } = string.Empty;
    public IReadOnlyList<string> Conditions { get; set; } = Array.Empty<string>();
    public bool FromCache { get; set; }
    public bool IsPersonalized { get; set; }
}
