using System.Security.Claims;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;

namespace Diagnova.Pages;

[Authorize]
public class ArtifactsModel : PageModel
{
    private readonly MongoDbService _mongoDb;

    public ArtifactsModel(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public MedicalProfile? UserProfile { get; set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            UserProfile = await _mongoDb.MedicalProfiles
                .Find(p => p.UserId == userId)
                .FirstOrDefaultAsync();
        }
    }

    // Handler for partial view loading (for SPA dashboard)
    public async Task<IActionResult> OnGetPartialAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            UserProfile = await _mongoDb.MedicalProfiles
                .Find(p => p.UserId == userId)
                .FirstOrDefaultAsync();
        }
        return Partial("_ArtifactsPartial", this);
    }

    public async Task<IActionResult> OnPostUpdateAsync([FromBody] UpdateFieldRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return new JsonResult(new { success = false, message = "User not authenticated" }) { StatusCode = 401 };
        }

        var profile = await _mongoDb.MedicalProfiles.Find(p => p.UserId == userId).FirstOrDefaultAsync();
        if (profile == null)
        {
            return new JsonResult(new { success = false, message = "Profile not found" }) { StatusCode = 404 };
        }

        switch (request.Field?.ToLower())
        {
            case "fullname": profile.FullName = request.Value; break;
            case "gender": profile.Gender = request.Value; break;
            case "weightkg": profile.WeightKg = double.TryParse(request.Value, out var w) ? w : profile.WeightKg; break;
            case "heightcm": profile.HeightCm = double.TryParse(request.Value, out var h) ? h : profile.HeightCm; break;
            case "bloodtype": profile.BloodType = request.Value; break;
            case "currentmedications": profile.CurrentMedications = request.Value; break;
            case "smokinghabit": profile.SmokingHabit = request.Value; break;
            case "alcoholhabit": profile.AlcoholHabit = request.Value; break;
            case "primarygoal": profile.PrimaryGoal = request.Value; break;
            case "activitylevel": profile.ActivityLevel = request.Value; break;
            case "dailycalorietarget": profile.DailyCalorieTarget = int.TryParse(request.Value, out var c) ? c : profile.DailyCalorieTarget; break;
            case "emergencycontactname": profile.EmergencyContactName = request.Value; break;
            case "emergencycontactemail": profile.EmergencyContactEmail = request.Value; break;
            case "countryregion": profile.CountryRegion = request.Value; break;
            case "recoverymethod": profile.RecoveryMethod = request.Value; break;
            default: break;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _mongoDb.MedicalProfiles.ReplaceOneAsync(p => p.Id == profile.Id, profile);

        return new JsonResult(new { success = true });
    }

    public class UpdateFieldRequest
    {
        public string? Field { get; set; }
        public string? Value { get; set; }
    }
}