using System.Security.Claims;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;

namespace Diagnova.Pages;

[Authorize]
public class HistoryModel : PageModel
{
    private readonly MongoDbService _mongoDb;

    public HistoryModel(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public IReadOnlyList<HistoryRow> Sessions { get; private set; } = Array.Empty<HistoryRow>();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var sessions = await _mongoDb.ChatSessions
            .Find(s => s.UserId == userId)
            .SortByDescending(s => s.StartedAt)
            .ToListAsync();

        var result = new List<HistoryRow>();
        foreach (var s in sessions)
        {
            var messageCount = await _mongoDb.ChatMessages
                .Find(m => m.SessionId == s.Id)
                .CountDocumentsAsync();

            result.Add(new HistoryRow(
                s.Id ?? string.Empty,
                s.StartedAt,
                s.ClosedAt,
                (int)messageCount));
        }

        Sessions = result;
    }
}

public sealed record HistoryRow(string Id, DateTimeOffset StartedAt, DateTimeOffset? ClosedAt, int MessageCount);