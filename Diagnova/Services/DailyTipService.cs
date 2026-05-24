using Diagnova.Models;
using MongoDB.Driver;

namespace Diagnova.Services;

public sealed class DailyTipService
{
    private readonly MongoDbService _mongoDb;
    private readonly IOpenAiChatService _ai;
    private readonly ILogger<DailyTipService> _logger;

    public DailyTipService(MongoDbService mongoDb, IOpenAiChatService ai, ILogger<DailyTipService> logger)
    {
        _mongoDb = mongoDb;
        _ai = ai;
        _logger = logger;
    }

    public async Task<DailyTipResponse> GetTodayTipAsync(string userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _mongoDb.DailyHealthTips
            .Find(t => t.UserId == userId && t.TipDate == today)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return ToResponse(existing, fromCache: true);

        var profile = await _mongoDb.MedicalProfiles
            .Find(p => p.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        var conditions = profile?.PreExistingConditions?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        var category = ConditionTipFallbacks.CategoryForToday();
        var profileContext = profile is not null ? MedicalProfileSummary.BuildCondensed(profile) : null;

        DailyTipGeneration? generated = null;
        if (conditions.Count > 0)
        {
            generated = await _ai.GenerateDailyConditionTipAsync(
                conditions, category, profileContext, cancellationToken);
        }

        string title;
        string body;
        if (generated is not null)
        {
            title = generated.Title;
            body = generated.Body;
            category = string.IsNullOrWhiteSpace(generated.Category) ? category : generated.Category;
        }
        else
        {
            (title, body) = ConditionTipFallbacks.Get(category, conditions);
        }

        var tip = new DailyHealthTip
        {
            UserId = userId,
            TipDate = today,
            Category = category,
            Title = title,
            Body = body,
            Conditions = conditions,
            GeneratedAt = DateTime.UtcNow,
        };

        await _mongoDb.DailyHealthTips.InsertOneAsync(tip, cancellationToken: cancellationToken);
        _logger.LogInformation("Daily tip created for user {UserId} ({Category})", userId, category);

        return ToResponse(tip, fromCache: false);
    }

    private static DailyTipResponse ToResponse(DailyHealthTip tip, bool fromCache) =>
        new()
        {
            Title = tip.Title,
            Body = tip.Body,
            Category = tip.Category,
            CategoryLabel = ConditionTipFallbacks.CategoryLabel(tip.Category),
            TipDate = tip.TipDate.ToString("yyyy-MM-dd"),
            Conditions = tip.Conditions,
            FromCache = fromCache,
            IsPersonalized = tip.Conditions.Count > 0,
        };
}
