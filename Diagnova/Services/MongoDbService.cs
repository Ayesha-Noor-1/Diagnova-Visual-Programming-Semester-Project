using Diagnova.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diagnova.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public MongoDbService(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<MedicalProfile> MedicalProfiles =>
        _database.GetCollection<MedicalProfile>("MedicalProfiles");

    public IMongoCollection<MongoChatSession> ChatSessions =>
        _database.GetCollection<MongoChatSession>("ChatSessions");

    public IMongoCollection<MongoChatMessage> ChatMessages =>
        _database.GetCollection<MongoChatMessage>("ChatMessages");

    public IMongoCollection<MongoVitalReading> VitalReadings =>
        _database.GetCollection<MongoVitalReading>("VitalReadings");
}

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ProfilesCollectionName { get; set; } = string.Empty;
    public string ChatSessionsCollectionName { get; set; } = string.Empty;
    public string ChatMessagesCollectionName { get; set; } = string.Empty;
    public string VitalReadingsCollectionName { get; set; } = string.Empty;
}