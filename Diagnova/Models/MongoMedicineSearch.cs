using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diagnova.Models;

public class MongoMedicineSearch
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public DateTimeOffset SearchedAt { get; set; }
    public string? BrandName { get; set; }
}