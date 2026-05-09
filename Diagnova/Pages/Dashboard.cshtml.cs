using System.Security.Claims;
using Diagnova.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Diagnova.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly AppDbContext _db;

    public DashboardModel(AppDbContext db)
    {
        _db = db;
    }

    public string? LatestAssistantSummary { get; private set; }

    public VitalReadingVm? LatestVitals { get; private set; }

    public int ChatSessionCount { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        LatestAssistantSummary = await (
            from m in _db.ChatMessages.AsNoTracking()
            join s in _db.ChatSessions.AsNoTracking() on m.SessionId equals s.Id
            where s.UserId == userId && m.Role == "assistant"
            orderby m.CreatedAt descending
            select m.Content
        ).FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(LatestAssistantSummary) && LatestAssistantSummary.Length > 280)
            LatestAssistantSummary = LatestAssistantSummary[..280].TrimEnd() + "…";

        var latest = await _db.VitalReadings.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.RecordedAt)
            .FirstOrDefaultAsync();

        if (latest != null)
        {
            LatestVitals = new VitalReadingVm(
                latest.RecordedAt,
                latest.SystolicMmHg,
                latest.DiastolicMmHg,
                latest.BloodSugarMgDl,
                latest.TemperatureC);
        }

        ChatSessionCount = await _db.ChatSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .CountAsync();
    }
}

public sealed record VitalReadingVm(
    DateTimeOffset RecordedAt,
    int? SystolicMmHg,
    int? DiastolicMmHg,
    double? BloodSugarMgDl,
    double? TemperatureC);
