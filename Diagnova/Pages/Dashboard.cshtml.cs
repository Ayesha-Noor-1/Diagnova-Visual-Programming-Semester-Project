using System.Security.Claims;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;

namespace Diagnova.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly MongoDbService _mongoDb;

    public DashboardModel(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public string? LatestAssistantSummary { get; private set; }

    public VitalReadingVm? LatestVitals { get; private set; }

    public long ChatSessionCount { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Get latest assistant message
        var latestMessages = await _mongoDb.ChatMessages
            .Find(m => m.Role == "assistant")
            .SortByDescending(m => m.CreatedAt)
            .Limit(1)
            .ToListAsync();

        if (latestMessages.Any())
        {
            LatestAssistantSummary = latestMessages.First().Content;
            if (!string.IsNullOrEmpty(LatestAssistantSummary) && LatestAssistantSummary.Length > 280)
                LatestAssistantSummary = LatestAssistantSummary[..280].TrimEnd() + "…";
        }

        // Get latest vitals
        var latestVitals = await _mongoDb.VitalReadings
            .Find(v => v.UserId == userId)
            .SortByDescending(v => v.RecordedAt)
            .FirstOrDefaultAsync();

        if (latestVitals != null)
        {
            LatestVitals = new VitalReadingVm(
                latestVitals.RecordedAt,
                latestVitals.SystolicMmHg,
                latestVitals.DiastolicMmHg,
                latestVitals.BloodSugarMgDl,
                latestVitals.TemperatureC);
        }

        // Count chat sessions
        ChatSessionCount = await _mongoDb.ChatSessions
            .Find(s => s.UserId == userId)
            .CountDocumentsAsync();
    }
}

public sealed record VitalReadingVm(
    DateTimeOffset RecordedAt,
    int? SystolicMmHg,
    int? DiastolicMmHg,
    double? BloodSugarMgDl,
    double? TemperatureC);