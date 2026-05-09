using System.Security.Claims;
using Diagnova.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Diagnova.Pages;

[Authorize]
public class HistoryModel : PageModel
{
    private readonly AppDbContext _db;

    public HistoryModel(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<HistoryRow> Sessions { get; private set; } = Array.Empty<HistoryRow>();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        Sessions = await _db.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new HistoryRow(
                s.Id,
                s.StartedAt,
                s.ClosedAt,
                s.Messages.Count))
            .ToListAsync();
    }
}

public sealed record HistoryRow(int Id, DateTimeOffset StartedAt, DateTimeOffset? ClosedAt, int MessageCount);
