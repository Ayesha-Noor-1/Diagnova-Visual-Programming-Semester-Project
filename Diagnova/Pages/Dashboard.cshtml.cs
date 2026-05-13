using System.Security.Claims;
using Diagnova.Models;
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
    public string? UserName { get; private set; }
    public int ThisWeekChats { get; private set; }
    public int VitalsCount { get; private set; }
    public int StreakDays { get; private set; }
    public List<ChatSessionSummary> RecentChatSessions { get; private set; } = new();
    public MedicalProfile? UserProfile { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        UserName = User.Identity?.Name ?? "User";

        // Get user profile
        UserProfile = await _mongoDb.MedicalProfiles.Find(p => p.UserId == userId).FirstOrDefaultAsync();

        // Get chat sessions
        var allSessions = await _mongoDb.ChatSessions
            .Find(s => s.UserId == userId)
            .SortByDescending(s => s.StartedAt)
            .ToListAsync();

        ChatSessionCount = allSessions.Count;

        // Calculate this week's chats
        var startOfWeek = DateTimeOffset.UtcNow.AddDays(-7);
        ThisWeekChats = allSessions.Count(s => s.StartedAt >= startOfWeek);

        // Get recent sessions for display
        RecentChatSessions = new List<ChatSessionSummary>();
        foreach (var session in allSessions.Take(10))
        {
            var messages = await _mongoDb.ChatMessages
                .Find(m => m.SessionId == session.Id)
                .SortBy(m => m.CreatedAt)
                .ToListAsync();

            var firstUserMessage = messages.FirstOrDefault(m => m.Role == "user");
            var title = firstUserMessage != null
                ? (firstUserMessage.Content.Length > 40
                    ? firstUserMessage.Content.Substring(0, 40) + "..."
                    : firstUserMessage.Content)
                : "New Conversation";

            RecentChatSessions.Add(new ChatSessionSummary
            {
                Id = session.Id ?? string.Empty,
                StartedAt = session.StartedAt,
                Title = title,
                MessageCount = messages.Count
            });
        }

        // Get vitals count
        VitalsCount = (int)await _mongoDb.VitalReadings.Find(v => v.UserId == userId).CountDocumentsAsync();

        // Calculate streak
        var last7Days = new List<DateTimeOffset>();
        for (int i = 0; i < 7; i++)
            last7Days.Add(DateTimeOffset.UtcNow.AddDays(-i).Date);

        var chatDates = allSessions.Select(s => s.StartedAt.Date).Distinct().ToList();
        var streak = 0;
        foreach (var day in last7Days)
        {
            if (chatDates.Contains(day.Date))
                streak++;
            else
                break;
        }
        StreakDays = streak;
    }
}

public class ChatSessionSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MessageCount { get; set; }
}

public sealed record VitalReadingVm(
    DateTimeOffset RecordedAt,
    int? SystolicMmHg,
    int? DiastolicMmHg,
    double? BloodSugarMgDl,
    double? TemperatureC);