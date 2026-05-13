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

    public string? CurrentSessionId { get; private set; }

    public async Task OnGet()
    {
        await LoadAsync();
    }

    // Handler for partial view loading (for SPA dashboard)
    public async Task<IActionResult> OnGetPartialAsync()
    {
        await LoadAsync();
        return Partial("_ChatPartial", this);
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

        var scan = _emergency.Scan(text);
        if (scan.IsEmergency)
        {
            var reply = BuildEmergencyReply(scan.MatchedKeywords);
            await _mongoDb.ChatMessages.InsertOneAsync(new MongoChatMessage
            {
                SessionId = session.Id!,
                Role = "assistant",
                Content = reply,
            });

            ShowEmergencyModal = true;
            EmergencyKeywords = scan.MatchedKeywords;
        }
        else
        {
            var profile = await _mongoDb.MedicalProfiles
                .Find(p => p.UserId == userId)
                .FirstOrDefaultAsync();
            var profileContext = BuildPatientProfileContext(profile);

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
            CurrentSessionId = null;
            return;
        }

        CurrentSessionId = session.Id;
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
        return sb.ToString().TrimEnd();
    }

    private static string BuildEmergencyReply(IReadOnlyList<string> keywords)
    {
        var kw = string.Join(", ", keywords);
        return $"""
            ## Emergency guidance

            Your message matched urgent keywords: **{kw}**.

            **Call emergency services immediately** if you are in danger or symptoms are severe.

            **Pakistan helplines (examples):** **1122** (Rescue / emergency services), **115** (Edhi Ambulance), **1166** (health helpline). Use the official local number for your city if different.

            Diagnova is not a substitute for emergency care. If in doubt, seek urgent medical attention or go to the nearest emergency department.
            """;
    }
}

public sealed record ChatLineVm(string Role, string Content);