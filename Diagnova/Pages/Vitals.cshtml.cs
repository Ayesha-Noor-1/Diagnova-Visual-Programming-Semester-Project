using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;

namespace Diagnova.Pages;

[Authorize]
public class VitalsModel : PageModel
{
    private readonly MongoDbService _mongoDb;

    public VitalsModel(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    [BindProperty]
    public VitalsInputModel Input { get; set; } = new();

    public List<VitalReadingDisplay> Recent { get; set; } = new();

    public string ChartJson { get; set; } = "[]";

    public class VitalsInputModel
    {
        public int? SystolicMmHg { get; set; }
        public int? DiastolicMmHg { get; set; }
        public double? BloodSugarMgDl { get; set; }
        public double? TemperatureC { get; set; }
        public string? Notes { get; set; }
    }

    public class VitalReadingDisplay
    {
        public DateTimeOffset RecordedAt { get; set; }
        public int? SystolicMmHg { get; set; }
        public int? DiastolicMmHg { get; set; }
        public double? BloodSugarMgDl { get; set; }
        public double? TemperatureC { get; set; }
        public string? Notes { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadReadingsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadReadingsAsync();
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var reading = new MongoVitalReading
        {
            UserId = userId,
            RecordedAt = DateTimeOffset.UtcNow,
            SystolicMmHg = Input.SystolicMmHg,
            DiastolicMmHg = Input.DiastolicMmHg,
            BloodSugarMgDl = Input.BloodSugarMgDl,
            TemperatureC = Input.TemperatureC,
            Notes = Input.Notes
        };

        await _mongoDb.VitalReadings.InsertOneAsync(reading);
        await LoadReadingsAsync();

        return Page();
    }

    private async Task LoadReadingsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var readings = await _mongoDb.VitalReadings
            .Find(v => v.UserId == userId)
            .SortByDescending(v => v.RecordedAt)
            .Limit(20)
            .ToListAsync();

        Recent = readings.Select(r => new VitalReadingDisplay
        {
            RecordedAt = r.RecordedAt,
            SystolicMmHg = r.SystolicMmHg,
            DiastolicMmHg = r.DiastolicMmHg,
            BloodSugarMgDl = r.BloodSugarMgDl,
            TemperatureC = r.TemperatureC,
            Notes = r.Notes
        }).ToList();

        // Prepare chart data
        var chartData = readings.OrderBy(r => r.RecordedAt).Select(r => new
        {
            t = r.RecordedAt.LocalDateTime.ToString("MM/dd"),
            bpSys = r.SystolicMmHg,
            glucose = r.BloodSugarMgDl
        });

        ChartJson = System.Text.Json.JsonSerializer.Serialize(chartData);
    }
}