using System.Security.Claims;
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
            var assistant = await _ai.GetAssistantReplyAsync(priorTurns, text, HttpContext.RequestAborted);
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