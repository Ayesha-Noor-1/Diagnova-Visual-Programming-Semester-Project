using System.Security.Claims;
using Diagnova.Data;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Diagnova.Pages;

[Authorize]
public class ChatModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IOpenAiChatService _ai;
    private readonly IEmergencyDetectorService _emergency;

    public ChatModel(AppDbContext db, IOpenAiChatService ai, IEmergencyDetectorService emergency)
    {
        _db = db;
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

        var priorRows = await _db.ChatMessages
            .Where(m => m.SessionId == session.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var priorTurns = priorRows
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => new ChatTurn(m.Role, m.Content))
            .ToList();

        _db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = text,
        });
        await _db.SaveChangesAsync();

        var scan = _emergency.Scan(text);
        if (scan.IsEmergency)
        {
            var reply = BuildEmergencyReply(scan.MatchedKeywords);
            _db.ChatMessages.Add(new ChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = reply,
            });
            await _db.SaveChangesAsync();

            ShowEmergencyModal = true;
            EmergencyKeywords = scan.MatchedKeywords;
        }
        else
        {
            var assistant = await _ai.GetAssistantReplyAsync(priorTurns, text, HttpContext.RequestAborted);
            _db.ChatMessages.Add(new ChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = assistant,
            });
            await _db.SaveChangesAsync();
        }

        UserInput = string.Empty;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostNewChatAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var open = await _db.ChatSessions
            .Where(s => s.UserId == userId && s.ClosedAt == null)
            .ToListAsync();

        foreach (var s in open)
            s.ClosedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var session = await _db.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ClosedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (session == null)
        {
            Messages = Array.Empty<ChatLineVm>();
            return;
        }

        Messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == session.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatLineVm(m.Role, m.Content))
            .ToListAsync();
    }

    private async Task<ChatSession> GetOrCreateActiveSessionAsync(string userId)
    {
        var open = await _db.ChatSessions
            .Where(s => s.UserId == userId && s.ClosedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (open != null)
            return open;

        open = new ChatSession { UserId = userId };
        _db.ChatSessions.Add(open);
        await _db.SaveChangesAsync();
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
