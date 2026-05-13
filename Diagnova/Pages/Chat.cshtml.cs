using System.Security.Claims;
using System.Text;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;

namespace Diagnova.Pages;

[Authorize]
public class ChatModel : PageModel
{
    private readonly MongoDbService _mongoDb;
    private readonly IOpenAiChatService _ai;
    private readonly IEmergencyDetectorService _emergency;

    public ChatModel(MongoDbService mongoDb, IOpenAiChatService ai, IEmergencyDetectorService emergency)
    {
        _mongoDb = mongoDb;
        _ai = ai;
        _emergency = emergency;
    }

    public IReadOnlyList<ChatLineVm> Messages { get; private set; } = Array.Empty<ChatLineVm>();

    [BindProperty]
    public string UserInput { get; set; } = string.Empty;

    public bool ShowEmergencyModal { get; set; }

    public IReadOnlyList<string> EmergencyKeywords { get; private set; } = Array.Empty<string>();

    /// <summary>AI explanation when alarming due to profile + symptoms (not keyword-only).</summary>
    public string? AlarmingReason { get; set; }

    public string? SpecialistHint { get; set; }

    public async Task OnGet()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var text = UserInput.Trim();
        if (string.IsNullOrEmpty(text))
        {
            await LoadAsync();
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var session = await GetOrCreateActiveSessionAsync(userId);

        var priorRows = await _mongoDb.ChatMessages
            .Find(m => m.SessionId == session.Id)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        var priorTurns = priorRows
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => new ChatTurn(m.Role, m.Content))
            .ToList();

        await _mongoDb.ChatMessages.InsertOneAsync(new MongoChatMessage
        {
            SessionId = session.Id!,
            Role = "user",
            Content = text,
        });

        var profile = await _mongoDb.MedicalProfiles
            .Find(p => p.UserId == userId)
            .FirstOrDefaultAsync();
        var profileContext = BuildPatientProfileContext(profile);

        var scan = _emergency.Scan(text);
        var alarming = scan.IsEmergency;
        var keywords = scan.MatchedKeywords.ToList();
        string? alarmingReason = null;
        string? specialistHint = null;

        if (!alarming)
        {
            var classification = await _ai.ClassifyAlarmingAsync(text, profileContext, HttpContext.RequestAborted);
            if (classification?.IsAlarming == true)
            {
                alarming = true;
                alarmingReason = classification.Reason;
                specialistHint = classification.SpecialistHint;
            }
        }

        if (alarming)
        {
            var reply = BuildEmergencyReply(keywords, alarmingReason, specialistHint);
            await _mongoDb.ChatMessages.InsertOneAsync(new MongoChatMessage
            {
                SessionId = session.Id!,
                Role = "assistant",
                Content = reply,
            });

            ShowEmergencyModal = true;
            EmergencyKeywords = keywords;
            AlarmingReason = alarmingReason;
            SpecialistHint = specialistHint;
        }
        else
        {
            var assistant = await _ai.GetAssistantReplyAsync(priorTurns, text, profileContext, HttpContext.RequestAborted);
            await _mongoDb.ChatMessages.InsertOneAsync(new MongoChatMessage
            {
                SessionId = session.Id!,
                Role = "assistant",
                Content = assistant,
            });
        }

        UserInput = string.Empty;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostEmergencyPlacesAsync(double lat, double lng)
    {
        try
        {
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
                return new JsonResult(new { ok = false, error = "Invalid coordinates." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return new JsonResult(new { ok = false, error = "Not signed in." });

            var session = await _mongoDb.ChatSessions
                .Find(s => s.UserId == userId && s.ClosedAt == null)
                .SortByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (session?.Id == null)
                return new JsonResult(new { ok = false, error = "No active chat session." });

            var recent = await _mongoDb.ChatMessages
                .Find(m => m.SessionId == session.Id)
                .SortByDescending(m => m.CreatedAt)
                .Limit(40)
                .ToListAsync();

            var lastUser = recent.FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (lastUser is null || string.IsNullOrWhiteSpace(lastUser.Content))
                return new JsonResult(new { ok = false, error = "No recent user message found." });

            var profile = await _mongoDb.MedicalProfiles
                .Find(p => p.UserId == userId)
                .FirstOrDefaultAsync();
            var profileContext = BuildPatientProfileContext(profile);

            var facilities = await _ai.SuggestEmergencyFacilitiesAsync(
                lat,
                lng,
                lastUser.Content,
                profileContext,
                HttpContext.RequestAborted);

            return new JsonResult(new
            {
                ok = true,
                patient = new { lat, lng },
                facilities = facilities.Select(f => new
                {
                    f.Name,
                    f.Address,
                    f.Latitude,
                    f.Longitude,
                    f.DistanceKm,
                    f.Notes,
                }),
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { ok = false, error = "Server error while loading places. " + ex.Message });
        }
    }

    public async Task<IActionResult> OnPostNewChatAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var open = await _mongoDb.ChatSessions
            .Find(s => s.UserId == userId && s.ClosedAt == null)
            .ToListAsync();

        foreach (var s in open)
        {
            s.ClosedAt = DateTimeOffset.UtcNow;
            await _mongoDb.ChatSessions.ReplaceOneAsync(
                filter: x => x.Id == s.Id,
                replacement: s);
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var session = await _mongoDb.ChatSessions
            .Find(s => s.UserId == userId && s.ClosedAt == null)
            .SortByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (session == null || session.Id == null)
        {
            Messages = Array.Empty<ChatLineVm>();
            return;
        }

        var messages = await _mongoDb.ChatMessages
            .Find(m => m.SessionId == session.Id)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        Messages = messages.Select(m => new ChatLineVm(m.Role, m.Content)).ToList();
    }

    private async Task<MongoChatSession> GetOrCreateActiveSessionAsync(string userId)
    {
        var open = await _mongoDb.ChatSessions
            .Find(s => s.UserId == userId && s.ClosedAt == null)
            .FirstOrDefaultAsync();

        if (open != null)
            return open;

        open = new MongoChatSession { UserId = userId };
        await _mongoDb.ChatSessions.InsertOneAsync(open);
        return open;
    }

    /// <summary>Formats MongoDB medical profile for the model (no email/phone/security fields).</summary>
    private static string? BuildPatientProfileContext(MedicalProfile? profile)
    {
        if (profile is null)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine($"- **Name:** {profile.FullName}");
        sb.AppendLine($"- **Age:** {profile.Age} (from profile date of birth)");
        sb.AppendLine($"- **Gender:** {profile.Gender}");
        if (profile.WeightKg > 0 && profile.HeightCm > 0)
            sb.AppendLine($"- **Weight / height:** {profile.WeightKg} kg, {profile.HeightCm} cm (BMI {profile.BMI})");
        if (!string.IsNullOrWhiteSpace(profile.BloodType))
            sb.AppendLine($"- **Blood type:** {profile.BloodType}");
        if (!string.IsNullOrWhiteSpace(profile.CurrentMedications))
            sb.AppendLine($"- **Current medications (self-reported):** {profile.CurrentMedications}");
        else
            sb.AppendLine("- **Current medications (self-reported):** *none listed in profile*");
        if (profile.PreExistingConditions.Count > 0)
            sb.AppendLine($"- **Pre-existing conditions:** {string.Join(", ", profile.PreExistingConditions)}");
        if (profile.Allergies.Count > 0)
            sb.AppendLine($"- **Allergies:** {string.Join(", ", profile.Allergies)}");
        sb.AppendLine($"- **Smoking:** {profile.SmokingHabit} · **Alcohol:** {profile.AlcoholHabit}");
        sb.AppendLine($"- **Activity level:** {profile.ActivityLevel}");
        if (!string.IsNullOrWhiteSpace(profile.PrimaryGoal))
            sb.AppendLine($"- **Health goal:** {profile.PrimaryGoal}");
        if (!string.IsNullOrWhiteSpace(profile.CountryRegion))
            sb.AppendLine($"- **Country / region:** {profile.CountryRegion}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildEmergencyReply(
        IReadOnlyList<string> keywords,
        string? profileAlarmingReason,
        string? specialistHint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Alarming situation");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(profileAlarmingReason))
            sb.AppendLine(profileAlarmingReason);
        else
            sb.AppendLine("Your message matches phrases that can indicate an **urgent** medical situation.");

        if (keywords.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**Matched urgent phrases:** {string.Join(", ", keywords)}.");
        }

        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(specialistHint))
            sb.AppendLine($"**Care routing:** Go to the **nearest emergency department now**. When you speak with triage, mention you may need **{specialistHint}** follow-up after stabilization.");
        else
            sb.AppendLine("**Care routing:** Go to the **nearest emergency department** or call emergency services **now**.");

        sb.AppendLine();
        sb.AppendLine("**Call emergency services immediately** if you are in danger, feel faint, have trouble breathing, or symptoms are severe.");
        sb.AppendLine();
        sb.AppendLine("**Pakistan helplines (examples):** **1122** (Rescue / emergency services), **115** (Edhi Ambulance), **1166** (health helpline). Use the official local number for your city if different.");
        sb.AppendLine();
        sb.AppendLine("Diagnova is not a substitute for emergency care. In the popup you can share your **approximate location** to see **AI-suggested** nearby facilities on a map — **verify by phone or official maps** before traveling.");
        return sb.ToString().TrimEnd();
    }
}

public sealed record ChatLineVm(string Role, string Content);
