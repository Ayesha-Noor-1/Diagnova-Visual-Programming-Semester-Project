using System.Security.Claims;
using System.Text.Json;
using Diagnova.Data;
using Diagnova.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Diagnova.Pages;

[Authorize]
public class VitalsModel : PageModel
{
    private readonly AppDbContext _db;

    public VitalsModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public VitalForm Input { get; set; } = new();

    public IReadOnlyList<VitalReading> Recent { get; private set; } = Array.Empty<VitalReading>();

    /// <summary>JSON array for Chart.js (oldest → newest).</summary>
    public string ChartJson { get; private set; } = "[]";

    public async Task OnGetAsync()
    {
        await LoadRecentAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRecentAsync();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var hasAny =
            Input.SystolicMmHg.HasValue ||
            Input.DiastolicMmHg.HasValue ||
            Input.BloodSugarMgDl.HasValue ||
            Input.TemperatureC.HasValue;

        if (!hasAny)
        {
            ModelState.AddModelError(string.Empty, "Enter at least one measurement.");
            return Page();
        }

        _db.VitalReadings.Add(new VitalReading
        {
            UserId = userId,
            RecordedAt = DateTimeOffset.UtcNow,
            SystolicMmHg = Input.SystolicMmHg,
            DiastolicMmHg = Input.DiastolicMmHg,
            BloodSugarMgDl = Input.BloodSugarMgDl,
            TemperatureC = Input.TemperatureC,
            Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
        });

        await _db.SaveChangesAsync();

        Input = new VitalForm();
        await LoadRecentAsync();
        return Page();
    }

    private async Task LoadRecentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Recent = await _db.VitalReadings.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.RecordedAt)
            .Take(30)
            .ToListAsync();

        var forChart = Recent.OrderBy(v => v.RecordedAt).Select(v => new
        {
            t = v.RecordedAt.LocalDateTime.ToString("MM/dd HH:mm"),
            bpSys = v.SystolicMmHg,
            glucose = v.BloodSugarMgDl,
        }).ToList();

        ChartJson = JsonSerializer.Serialize(forChart);
    }

    public sealed class VitalForm
    {
        public int? SystolicMmHg { get; set; }

        public int? DiastolicMmHg { get; set; }

        public double? BloodSugarMgDl { get; set; }

        public double? TemperatureC { get; set; }

        public string? Notes { get; set; }
    }
}
